using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EcomAI.Platform.Infrastructure.Services.Marketing;

/// <summary>
/// Loads /skills/*.md files from the API host content root and concatenates them
/// into a single system prompt block. Cached in IMemoryCache for 1 hour.
/// </summary>
public sealed class SkillLoaderService : ISkillLoaderService
{
    private const string CacheKey = "marketing:system-prompt";

    private readonly IHostEnvironment             _env;
    private readonly IMemoryCache                 _cache;
    private readonly IConfiguration               _config;
    private readonly ILogger<SkillLoaderService>  _logger;

    public SkillLoaderService(
        IHostEnvironment env,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<SkillLoaderService> logger)
    {
        _env    = env;
        _cache  = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<string> LoadSystemPromptAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out string? cached) && cached is not null)
            return cached;

        var relativePath = _config.GetValue<string>("MarketingEngine:SkillsFolderRelativePath", "skills");
        var skillsDir    = Path.Combine(_env.ContentRootPath, relativePath!);

        if (!Directory.Exists(skillsDir))
        {
            _logger.LogWarning("Skills directory not found at {Path}. Using default system prompt.", skillsDir);
            var defaultPrompt = DefaultSystemPrompt();
            _cache.Set(CacheKey, defaultPrompt, TimeSpan.FromHours(1));
            return defaultPrompt;
        }

        var files = Directory.GetFiles(skillsDir, "*.md", SearchOption.TopDirectoryOnly);
        Array.Sort(files); // Alphabetical — 01_identity.md, 02_... etc.

        var sb = new StringBuilder();
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                sb.AppendLine(content.Trim());
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read skill file {File}.", file);
            }
        }

        var prompt = sb.Length > 0 ? sb.ToString().Trim() : DefaultSystemPrompt();
        _cache.Set(CacheKey, prompt, TimeSpan.FromHours(1));

        _logger.LogInformation("Loaded {Count} skill files from {Dir}.", files.Length, skillsDir);
        return prompt;
    }

    private static string DefaultSystemPrompt() =>
        """
        You are an autonomous marketing agent for an e-commerce business.
        Your role is to analyze campaign performance data and recommend precise, data-driven actions.
        Always respond in the JSON format specified. Never take destructive actions.
        Prefer conservative, incremental adjustments. When uncertain, choose no_action.
        """;
}
