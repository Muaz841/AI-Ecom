using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;




public sealed record ToolDefinition(
    string Name,
    string Description,
    string ParametersSchema);


public sealed record ToolCall(string ToolName, string ArgumentsJson);


public sealed record ToolResult(
    string ToolName,
    bool Success,
    string ResultJson,
    string? ErrorMessage = null);



public interface IToolHandler
{
    string ToolName { get; }
    string Description { get; }
    string ParametersSchema { get; }

    Task<ToolResult> ExecuteAsync(
        Guid tenantId,
        string argumentsJson,
        CancellationToken ct = default);
}



public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> GetAll();
    IToolHandler? Resolve(string toolName);
}

public interface IToolExecutor
{
    Task<ToolResult> ExecuteAsync(
        Guid tenantId,
        ToolCall call,
        CancellationToken ct = default);
}


public sealed record AgentContentPart(
    string Role,                         
    string? Text = null,
    ToolCall? FunctionCall = null,        
    ToolResult? FunctionResponse = null); 


public sealed record AgentTurnRequest(
    string? SystemPrompt,
    IReadOnlyList<AgentContentPart> Contents,
    IReadOnlyList<ToolDefinition> Tools);


public sealed record AgentTurnResult(
    bool Success,
    string? TextReply,
    ToolCall? FunctionCall,
    int InputTokens,
    int OutputTokens,
    string? ErrorMessage = null);



public sealed record AgentRequest(
    Guid TenantId,
    string MessageContent,
    string DetectedIntent,
    string MessageIdForAudit,
    string? SystemPrompt = null);

public sealed record AgentResult(
    bool Success,
    string? FinalReply,
    IReadOnlyList<ToolCall> ToolCallsMade,
    int TotalInputTokens,
    int TotalOutputTokens,
    string? ErrorMessage = null);

public interface IAgentOrchestrator
{
    Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct = default);
}
