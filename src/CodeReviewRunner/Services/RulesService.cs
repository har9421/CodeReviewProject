using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;
using Newtonsoft.Json;

namespace CodeReviewRunner.Services;

public class RulesService : IRulesService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RulesService> _logger;
    private readonly CodeReviewOptions _options;

    public RulesService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<RulesService> logger,
        IOptions<CodeReviewOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<CodingRule>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        // Try to get from cache first
        if (_options.Rules.ValidationEnabled)
        {
            var cachedRules = await GetCachedRulesAsync(cancellationToken);
            if (cachedRules != null)
            {
                _logger.LogDebug("Using cached rules ({RuleCount} rules)", cachedRules.Count);
                return cachedRules;
            }
        }

        // Default rules if no URL provided
        var defaultRules = GetDefaultRules();
        await CacheRulesAsync(defaultRules, cancellationToken);
        return defaultRules;
    }

    public async Task<List<CodingRule>> GetRulesFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching rules from {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var rules = JsonConvert.DeserializeObject<List<CodingRule>>(json) ?? new List<CodingRule>();

            _logger.LogInformation("Successfully fetched {RuleCount} rules from {Url}", rules.Count, url);

            // Cache the rules
            await CacheRulesAsync(rules, cancellationToken);

            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch rules from {Url}", url);
            throw;
        }
    }

    public Task<bool> ValidateRulesAsync(List<CodingRule> rules, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!rules.Any())
            {
                _logger.LogWarning("No rules provided for validation");
                return Task.FromResult(false);
            }

            var validRules = 0;
            var invalidRules = 0;

            foreach (var rule in rules)
            {
                if (IsValidRule(rule))
                {
                    validRules++;
                }
                else
                {
                    invalidRules++;
                    _logger.LogWarning("Invalid rule found: {RuleId} - {Reason}",
                        rule.Id, GetValidationError(rule));
                }
            }

            _logger.LogInformation("Rule validation completed: {ValidRules} valid, {InvalidRules} invalid",
                validRules, invalidRules);

            return Task.FromResult(invalidRules == 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule validation");
            return Task.FromResult(false);
        }
    }

    public Task CacheRulesAsync(List<CodingRule> rules, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = "coding_rules";
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.Rules.CacheTimeoutMinutes),
                Priority = CacheItemPriority.High
            };

            _cache.Set(cacheKey, rules, cacheOptions);
            _logger.LogDebug("Cached {RuleCount} rules for {Minutes} minutes",
                rules.Count, _options.Rules.CacheTimeoutMinutes);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache rules");
            return Task.CompletedTask;
        }
    }

    public Task<List<CodingRule>?> GetCachedRulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = "coding_rules";
            return Task.FromResult(_cache.Get<List<CodingRule>>(cacheKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve cached rules");
            return Task.FromResult<List<CodingRule>?>(null);
        }
    }

    private List<CodingRule> GetDefaultRules()
    {
        return new List<CodingRule>
        {
            new()
            {
                Id = "method-name-length",
                Name = "Method Name Length",
                Description = "Method names should not exceed 30 characters",
                Severity = "warning",
                Category = "Naming",
                Languages = new List<string> { "csharp" },
                Pattern = "method.*name.*length",
                Message = "Method name should not exceed 30 characters",
                Suggestion = "Consider shortening the method name",
                Enabled = true,
                Priority = 1,
                Tags = new List<string> { "naming", "length" }
            },
            new()
            {
                Id = "component-name-length",
                Name = "Component Name Length",
                Description = "Component names should not exceed 25 characters",
                Severity = "error",
                Category = "Naming",
                Languages = new List<string> { "typescript", "javascript" },
                Pattern = "component.*name.*length",
                Message = "Component name should not exceed 25 characters",
                Suggestion = "Consider shortening the component name",
                Enabled = true,
                Priority = 1,
                Tags = new List<string> { "naming", "length", "react" }
            },
            new()
            {
                Id = "unused-variable",
                Name = "Unused Variable",
                Description = "Unused variables should be removed",
                Severity = "warning",
                Category = "Code Quality",
                Languages = new List<string> { "csharp", "javascript", "typescript" },
                Pattern = "unused.*variable",
                Message = "Unused variable detected",
                Suggestion = "Remove the unused variable or use it",
                Enabled = true,
                Priority = 2,
                Tags = new List<string> { "unused", "cleanup" }
            }
        };
    }

    private bool IsValidRule(CodingRule rule)
    {
        return !string.IsNullOrWhiteSpace(rule.Id) &&
               !string.IsNullOrWhiteSpace(rule.Name) &&
               !string.IsNullOrWhiteSpace(rule.Severity) &&
               rule.Languages.Any() &&
               !string.IsNullOrWhiteSpace(rule.Pattern);
    }

    private string GetValidationError(CodingRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Id))
            return "Missing ID";
        if (string.IsNullOrWhiteSpace(rule.Name))
            return "Missing Name";
        if (string.IsNullOrWhiteSpace(rule.Severity))
            return "Missing Severity";
        if (!rule.Languages.Any())
            return "Missing Languages";
        if (string.IsNullOrWhiteSpace(rule.Pattern))
            return "Missing Pattern";

        return "Unknown validation error";
    }
}
