using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.AI;

/// <summary>
/// Drives a multi-step tool-calling loop:
/// 1. Inject tool definitions into system prompt.
/// 2. Call the AI model via GenerateReplyAsync.
/// 3. Parse response for a tool call JSON block.
/// 4. Execute the tool, append result to context, repeat (max MaxIterations).
/// 5. Return the final text response.
///
/// Tool invocation format the AI must produce:
/// {"tool_call":{"name":"&lt;tool_name&gt;","args":{...}}}
/// </summary>
public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private const int MaxIterations = 5;

    private readonly IAIService _aiService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutor _toolExecutor;
    private readonly IApplicationLogger _logger;

    public AgentOrchestrator(
        IAIService aiService,
        IToolRegistry toolRegistry,
        IToolExecutor toolExecutor,
        IApplicationLogger logger)
    {
        _aiService = aiService;
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
        _logger = logger;
    }

    public async Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        var toolCallsMade = new List<ToolCall>();
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        // Build enhanced system prompt with tool definitions
        var systemPrompt = BuildSystemPromptWithTools(request.SystemPrompt);

        // Accumulate context across iterations
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine(request.MessageContent);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var currentContext = contextBuilder.ToString().Trim();

            var replyResult = await _aiService.GenerateReplyAsync(
                new ReplyRequest(
                    currentContext,
                    request.DetectedIntent,
                    request.InventoryContext,
                    request.MessageIdForAudit,
                    systemPrompt),
                cancellationToken: ct);

            totalInputTokens += replyResult.InputTokensUsed;
            totalOutputTokens += replyResult.OutputTokensUsed;

            if (!replyResult.Success || string.IsNullOrWhiteSpace(replyResult.GeneratedReply))
            {
                return new AgentResult(false, null, toolCallsMade, totalInputTokens, totalOutputTokens,
                    replyResult.ErrorMessage ?? "AI returned empty response.");
            }

            var rawResponse = replyResult.GeneratedReply.Trim();

            // Check if response contains a tool call
            var toolCall = TryParseToolCall(rawResponse);
            if (toolCall is null)
            {
                // No tool call — this is the final answer
                _logger.Info("AgentOrchestrator completed in {Iteration} iteration(s) for message {MessageId}. " +
                             "Tools used: {ToolCount}. Tokens: in={In} out={Out}",
                    iteration + 1, request.MessageIdForAudit, toolCallsMade.Count,
                    totalInputTokens, totalOutputTokens);

                return new AgentResult(true, rawResponse, toolCallsMade,
                    totalInputTokens, totalOutputTokens);
            }

            // Execute the tool and append result to context
            toolCallsMade.Add(toolCall);
            _logger.Info("Agent tool call: {ToolName} | iteration={Iter} | messageId={MsgId}",
                toolCall.ToolName, iteration + 1, request.MessageIdForAudit);

            var toolResult = await _toolExecutor.ExecuteAsync(request.TenantId, toolCall, ct);

            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"[Tool: {toolCall.ToolName}]");
            contextBuilder.AppendLine($"Result: {toolResult.ResultJson}");
            contextBuilder.AppendLine("Based on the above tool result, now provide your final response to the customer.");
        }

        _logger.Warning("AgentOrchestrator hit max iterations ({Max}) for message {MessageId}. Returning last response.",
            MaxIterations, request.MessageIdForAudit);

        return new AgentResult(false, null, toolCallsMade, totalInputTokens, totalOutputTokens,
            "Maximum agent iterations reached without a final response.");
    }

    private string BuildSystemPromptWithTools(string? tenantSystemPrompt)
    {
        var tools = _toolRegistry.GetAll();
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(tenantSystemPrompt))
        {
            sb.AppendLine(tenantSystemPrompt);
            sb.AppendLine();
        }

        if (tools.Count > 0)
        {
            sb.AppendLine("AVAILABLE TOOLS:");
            sb.AppendLine("You may call one of the following tools by responding with ONLY this JSON and nothing else:");
            sb.AppendLine("""{"tool_call":{"name":"<tool_name>","args":{<args>}}}""");
            sb.AppendLine("If you need to use a tool, output only the JSON above. Do NOT include any other text.");
            sb.AppendLine("If you do not need a tool, respond normally with your final answer.");
            sb.AppendLine();
            sb.AppendLine("Tool list:");

            foreach (var tool in tools)
            {
                sb.AppendLine($"- {tool.Name}: {tool.Description} | schema: {tool.ParametersSchema}");
            }

            sb.AppendLine();
            sb.AppendLine("IMPORTANT: Only call a tool when you genuinely need the information to answer the customer. " +
                          "Never call a tool that accesses another tenant's data. " +
                          "Do not call more than one tool per response.");
        }

        return sb.ToString().Trim();
    }

    private static ToolCall? TryParseToolCall(string response)
    {
        // Look for {"tool_call":{"name":"...","args":{...}}} pattern
        var trimmed = response.Trim();
        if (!trimmed.StartsWith("{")) return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (!doc.RootElement.TryGetProperty("tool_call", out var toolCallNode)) return null;

            var name = toolCallNode.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(name)) return null;

            var argsJson = toolCallNode.TryGetProperty("args", out var argsProp)
                ? argsProp.GetRawText()
                : "{}";

            return new ToolCall(name, argsJson);
        }
        catch
        {
            return null;
        }
    }
}
