using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

// ── Tool primitive types ───────────────────────────────────────────────────────

/// <summary>Describes a tool the AI agent can call.</summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    string ParametersSchema);

/// <summary>A tool invocation parsed from the AI's response.</summary>
public sealed record ToolCall(string ToolName, string ArgumentsJson);

/// <summary>Result returned after executing a tool.</summary>
public sealed record ToolResult(
    string ToolName,
    bool Success,
    string ResultJson,
    string? ErrorMessage = null);

// ── Tool handler (one per tool) ───────────────────────────────────────────────

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

// ── Registry & executor ───────────────────────────────────────────────────────

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

// ── Agent orchestrator ────────────────────────────────────────────────────────

public sealed record AgentRequest(
    Guid TenantId,
    string MessageContent,
    string DetectedIntent,
    string InventoryContext,
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
