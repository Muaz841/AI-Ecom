using System.Collections.Generic;
using System.Linq;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Infrastructure.AI;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers;

    public ToolRegistry(IEnumerable<IToolHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.ToolName, System.StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ToolDefinition> GetAll()
        => _handlers.Values
            .Select(h => new ToolDefinition(h.ToolName, h.Description, h.ParametersSchema))
            .ToList();

    public IToolHandler? Resolve(string toolName)
        => _handlers.TryGetValue(toolName, out var handler) ? handler : null;
}
