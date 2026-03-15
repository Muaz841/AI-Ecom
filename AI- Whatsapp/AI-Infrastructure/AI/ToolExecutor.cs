using System;
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
            return new ToolResult(call.ToolName, false, "{}", $"Tool '{call.ToolName}' is not available.");
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
            return new ToolResult(call.ToolName, false, "{}", ex.Message);
        }
    }
}
