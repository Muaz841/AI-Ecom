using System;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

/// <summary>
/// Builds fully-assembled AI prompts for a given tenant, injecting the
/// tenant's system prompt, brand rules, tone, and safety constraints.
/// </summary>
public interface ITenantPromptBuilder
{
    /// <summary>
    /// Returns the composite system prompt for this tenant.
    /// Combines: system prompt + tone + brand rules + forbidden topics + safety policy.
    /// Returns null when no profile is configured (fall back to generic prompt).
    /// </summary>
    Task<string?> GetSystemPromptAsync(Guid tenantId, CancellationToken ct = default);
}
