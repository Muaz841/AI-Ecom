using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.AI;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IApplicationLogger _logger;

    public ToolExecutor(IToolRegistry registry, IApplicationLogger logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteAsync(Guid tenantId, ToolCall call, CancellationToken ct = default)
    {
        var handler = _registry.Resolve(call.ToolName);
        if (handler is null)
        {
            _logger.Warning("Tool '{ToolName}' not found in registry for tenant {TenantId}.", call.ToolName, tenantId);
            var notFoundJson = JsonSerializer.Serialize(new
            {
                error = $"Tool '{call.ToolName}' is not available.",
                suggestion = "Respond to the customer based on what you already know."
            });
            return new ToolResult(call.ToolName, false, notFoundJson, $"Tool '{call.ToolName}' is not available.");
        }

        try
        {
            _logger.Info("Executing tool '{ToolName}' for tenant {TenantId}. Args: {Args}",
                call.ToolName, tenantId, call.ArgumentsJson);

            var result = await handler.ExecuteAsync(tenantId, call.ArgumentsJson, ct);

            _logger.Info("Tool '{ToolName}' completed for tenant {TenantId}. Success={Success}",
                call.ToolName, tenantId, result.Success);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Tool '{ToolName}' threw an exception for tenant {TenantId}.", call.ToolName, tenantId);
            var errorJson = JsonSerializer.Serialize(new
            {
                error = $"Tool '{call.ToolName}' encountered an unexpected error.",
                suggestion = "Apologise to the customer and ask them to try again or contact support."
            });
            return new ToolResult(call.ToolName, false, errorJson, ex.Message);
        }
    }
}
