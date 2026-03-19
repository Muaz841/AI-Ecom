using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.AI;

/// <summary>
/// Drives a native multi-turn tool-calling loop using the provider's first-class
/// function-calling API (Gemini functionDeclarations / functionResponse).
///
/// Loop per iteration:
/// 1. Call GenerateAgentTurnAsync with full conversation history + tool definitions.
/// 2. FunctionCall response → execute via IToolExecutor, append functionCall +
///    functionResponse to history, repeat.
/// 3. TextReply response → done; return as final answer.
/// 4. Stop after MaxIterations regardless.
/// </summary>
public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private const int MaxIterations = 5;

    private readonly IAIService _aiService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly IAiRuntimeConfigProvider _runtimeConfig;
    private readonly IApplicationLogger _logger;

    public AgentOrchestrator(
        IAIService aiService,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        IAiRuntimeConfigProvider runtimeConfig,
        IApplicationLogger logger)
    {
        _aiService = aiService;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _runtimeConfig = runtimeConfig;
        _logger = logger;
    }

    public async Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var toolCallsMade     = new List<ToolCall>();
        var totalInputTokens  = 0;
        var totalOutputTokens = 0;

        var rt = await _runtimeConfig.GetRuntimeConfigAsync(ct);
        IReadOnlyList<ToolDefinition> tools = (rt?.EnableToolCalling ?? true)
            ? _toolRegistry.GetAll()
            : [];

        // Initialise conversation with the user's message
        var contents = new List<AgentContentPart>
        {
            new("user", Text: request.MessageContent),
        };

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var turn = await _aiService.GenerateAgentTurnAsync(
                new AgentTurnRequest(request.SystemPrompt, contents, tools),
                ct);

            totalInputTokens  += turn.InputTokens;
            totalOutputTokens += turn.OutputTokens;

            if (!turn.Success)
                return new AgentResult(false, null, toolCallsMade,
                    totalInputTokens, totalOutputTokens,
                    turn.ErrorMessage ?? "AI turn failed.");

            // ── Final text answer ─────────────────────────────────────────────
            if (turn.TextReply is not null)
            {
                _logger.Info(
                    "AgentOrchestrator completed in {Iter} iteration(s) for {MsgId}. " +
                    "Tools={Count} Tokens: in={In} out={Out}",
                    iteration + 1, request.MessageIdForAudit,
                    toolCallsMade.Count, totalInputTokens, totalOutputTokens);

                return new AgentResult(true, turn.TextReply, toolCallsMade,
                    totalInputTokens, totalOutputTokens);
            }

            // ── Native function call ──────────────────────────────────────────
            if (turn.FunctionCall is not null)
            {
                var toolCall = turn.FunctionCall;
                toolCallsMade.Add(toolCall);

                _logger.Info(
                    "Agent native tool call: {Tool} | iteration={Iter} | messageId={MsgId}",
                    toolCall.ToolName, iteration + 1, request.MessageIdForAudit);

                // Append model's functionCall to conversation history
                contents.Add(new AgentContentPart("model", FunctionCall: toolCall));

                // Execute the tool
                var toolResult = await _toolExecutor.ExecuteAsync(request.TenantId, toolCall, ct);

                // Append functionResponse — Gemini expects role "user" for tool results
                contents.Add(new AgentContentPart("user", FunctionResponse: toolResult));

                continue;
            }

            // Neither TextReply nor FunctionCall — unexpected empty response
            return new AgentResult(false, null, toolCallsMade,
                totalInputTokens, totalOutputTokens,
                "AI returned an empty response (no text and no function call).");
        }

        _logger.Warning(
            "AgentOrchestrator hit max iterations ({Max}) for message {MsgId}.",
            MaxIterations, request.MessageIdForAudit);

        return new AgentResult(false, null, toolCallsMade,
            totalInputTokens, totalOutputTokens,
            "Maximum agent iterations reached without a final response.");
    }
}
