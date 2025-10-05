using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class IntelligentCodeAnalyzerService : ICodeAnalyzer
{
    private readonly ILogger<IntelligentCodeAnalyzerService> _logger;
    private readonly ILearningService _learningService;
    private readonly ConcurrentDictionary<string, List<CodingRule>> _cachedRules = new();
    private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
    private readonly SemaphoreSlim _analysisSemaphore;

    public IntelligentCodeAnalyzerService(ILogger<IntelligentCodeAnalyzerService> logger, ILearningService learningService)
    {
        _logger = logger;
        _learningService = learningService;
        _analysisSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
    }

    public async Task<List<CodeIssue>> AnalyzeFileAsync(FileChange fileChange)
    {
        await _analysisSemaphore.WaitAsync();
        try
        {
            var issues = new List<CodeIssue>();

            if (string.IsNullOrEmpty(fileChange.Content))
            {
                _logger.LogWarning("No content found for file {FilePath}", fileChange.Path);
                return issues;
            }

            try
            {
                _logger.LogInformation("Analyzing file {FilePath} - AnalyzeOnlyChangedLines: {AnalyzeOnlyChangedLines}, ChangedLinesCount: {ChangedLinesCount}",
                    fileChange.Path, fileChange.AnalyzeOnlyChangedLines, fileChange.ChangedLines.Count);

                var rules = await LoadCodingRulesAsync();
                var lines = fileChange.Content.Split('\n');

                // Use parallel processing for rule analysis
                var ruleTasks = rules.Select(rule => AnalyzeRuleAsync(fileChange, lines, rule));
                var ruleResults = await Task.WhenAll(ruleTasks);

                foreach (var ruleIssues in ruleResults)
                {
                    issues.AddRange(ruleIssues);
                }

                // Apply intelligent filtering based on learning data
                var repositoryName = ExtractRepositoryName(fileChange.Path);
                issues = await _learningService.FilterIssuesByRelevanceAsync(issues, repositoryName);

                _logger.LogInformation("Found {IssueCount} relevant issues in file {FilePath} (after filtering)", issues.Count, fileChange.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing file {FilePath}", fileChange.Path);
            }

            return issues;
        }
        finally
        {
            _analysisSemaphore.Release();
        }
    }

    public async Task<List<CodingRule>> LoadCodingRulesAsync()
    {
        var cacheKey = "default_rules";

        if (_cachedRules.TryGetValue(cacheKey, out var cachedRules) &&
            _cacheTimestamps.TryGetValue(cacheKey, out var timestamp) &&
            DateTime.UtcNow - timestamp < TimeSpan.FromMinutes(60))
        {
            return cachedRules;
        }

        try
        {
            var rules = new List<CodingRule>();

            // Load default rules
            var defaultRules = await LoadDefaultRulesAsync();
            rules.AddRange(defaultRules);

            // Load adaptive rules from learning service
            var adaptiveRules = await _learningService.GetAdaptiveRulesAsync();
            var convertedAdaptiveRules = adaptiveRules.Select(ar => new CodingRule
            {
                Id = ar.Id,
                Severity = ar.Severity,
                Message = ar.Message,
                Pattern = ar.Pattern,
                Suggestion = ar.Suggestion,
                Languages = new[] { "csharp" },
                AppliesTo = new[] { "*.cs" }
            });
            rules.AddRange(convertedAdaptiveRules);

            _cachedRules[cacheKey] = rules;
            _cacheTimestamps[cacheKey] = DateTime.UtcNow;

            _logger.LogInformation("Loaded {RuleCount} coding rules ({DefaultCount} default, {AdaptiveCount} adaptive)",
                rules.Count, defaultRules.Count, adaptiveRules.Count);

            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load coding standards");
            return GetDefaultRules();
        }
    }

    private async Task<List<CodeIssue>> AnalyzeRuleAsync(FileChange fileChange, string[] lines, CodingRule rule)
    {
        var issues = new List<CodeIssue>();

        try
        {
            if (string.IsNullOrEmpty(rule.Pattern))
                return issues;

            var regex = new Regex(rule.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // If analyzing only changed lines and there are no changed lines, skip analysis entirely
            if (fileChange.AnalyzeOnlyChangedLines && !fileChange.ChangedLines.Any())
            {
                _logger.LogInformation("Skipping analysis for file {FilePath} - no changed lines detected", fileChange.Path);
                return issues;
            }

            // Use parallel processing for line analysis
            var lineTasks = new List<Task<List<CodeIssue>>>();

            for (int i = 0; i < lines.Length; i++)
            {
                // If analyzing only changed lines, skip lines that weren't changed
                if (fileChange.AnalyzeOnlyChangedLines && fileChange.ChangedLines.Any() && !fileChange.ChangedLines.Contains(i + 1))
                {
                    continue;
                }

                var lineNumber = i;
                var line = lines[i];
                lineTasks.Add(Task.Run(() => AnalyzeLineAsync(rule, regex, fileChange.Path, lineNumber + 1, line)));
            }

            var lineResults = await Task.WhenAll(lineTasks);
            foreach (var lineIssues in lineResults)
            {
                issues.AddRange(lineIssues);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing rule {RuleId} for file {FilePath}", rule.Id, fileChange.Path);
        }

        return issues;
    }

    private async Task<List<CodeIssue>> AnalyzeLineAsync(CodingRule rule, Regex regex, string filePath, int lineNumber, string line)
    {
        var issues = new List<CodeIssue>();

        try
        {
            var matches = regex.Matches(line);
            foreach (Match match in matches)
            {
                // Apply context-aware analysis
                if (await IsValidMatchAsync(rule, match, line, filePath))
                {
                    issues.Add(new CodeIssue
                    {
                        RuleId = rule.Id,
                        Severity = rule.Severity,
                        Message = rule.Message,
                        Suggestion = rule.Suggestion,
                        FilePath = filePath,
                        LineNumber = lineNumber
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing line {LineNumber} in file {FilePath}", lineNumber, filePath);
        }

        return issues;
    }

    private async Task<bool> IsValidMatchAsync(CodingRule rule, Match match, string line, string filePath)
    {
        // Apply intelligent filtering based on context
        var context = GetLineContext(line, match);

        // Skip if it's a comment explaining why the code is intentionally written this way
        if (IsIntentionallyIgnored(line, context))
        {
            return false;
        }

        // Skip if it's a test file and the rule doesn't apply to tests
        if (IsTestFile(filePath) && !ShouldApplyToTests(rule))
        {
            return false;
        }

        // Apply learning-based confidence scoring
        var confidence = await _learningService.CalculateRuleConfidenceAsync(rule.Id);
        return confidence >= 0.3; // Minimum confidence threshold
    }

    private string GetLineContext(string line, Match match)
    {
        var start = Math.Max(0, match.Index - 50);
        var length = Math.Min(100, line.Length - start);
        return line.Substring(start, length);
    }

    private bool IsIntentionallyIgnored(string line, string context)
    {
        var ignorePatterns = new[]
        {
            @"//\s*intentionally",
            @"//\s*by\s*design",
            @"//\s*TODO.*fix",
            @"//\s*FIXME",
            @"//\s*HACK",
            @"//\s*workaround"
        };

        return ignorePatterns.Any(pattern => Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase));
    }

    private bool IsTestFile(string filePath)
    {
        var testPatterns = new[]
        {
            @"\btest\b",
            @"\btests\b",
            @"\bspec\b",
            @"\bspecs\b",
            @"\.test\.",
            @"\.spec\."
        };

        return testPatterns.Any(pattern => Regex.IsMatch(filePath, pattern, RegexOptions.IgnoreCase));
    }

    private bool ShouldApplyToTests(CodingRule rule)
    {
        // Some rules should not apply to test files
        var testExcludedRules = new[]
        {
            "naming-convention",
            "method-length",
            "class-length"
        };

        return !testExcludedRules.Contains(rule.Id);
    }

    private string ExtractRepositoryName(string filePath)
    {
        // Extract repository name from file path
        var parts = filePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "unknown";
    }

    private async Task<List<CodingRule>> LoadDefaultRulesAsync()
    {
        try
        {
            var rulesJson = await File.ReadAllTextAsync("coding-standards.json");
            var rules = JsonSerializer.Deserialize<List<CodingRule>>(rulesJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return rules ?? new List<CodingRule>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load default coding standards");
            return GetDefaultRules();
        }
    }

    private List<CodingRule> GetDefaultRules()
    {
        return new List<CodingRule>
        {
            new CodingRule
            {
                Id = "no-console-writeline",
                Severity = "warning",
                Message = "Avoid using Console.WriteLine in production code",
                Pattern = @"Console\.WriteLine\s*\(",
                Suggestion = "Use proper logging framework instead",
                Languages = new[] { "csharp" },
                AppliesTo = new[] { "*.cs" }
            },
            new CodingRule
            {
                Id = "no-magic-numbers",
                Severity = "info",
                Message = "Consider using named constants instead of magic numbers",
                Pattern = @"\b\d{3,}\b",
                Suggestion = "Define a named constant for this value",
                Languages = new[] { "csharp" },
                AppliesTo = new[] { "*.cs" }
            },
            new CodingRule
            {
                Id = "method-too-long",
                Severity = "warning",
                Message = "Method is too long and should be refactored",
                Pattern = @"public\s+\w+\s+\w+\s*\([^)]*\)\s*\{[^}]{500,}\}",
                Suggestion = "Break this method into smaller, more focused methods",
                Languages = new[] { "csharp" },
                AppliesTo = new[] { "*.cs" }
            },
            new CodingRule
            {
                Id = "missing-documentation",
                Severity = "info",
                Message = "Public methods should have XML documentation",
                Pattern = @"public\s+\w+\s+\w+\s*\([^)]*\)\s*(?!\s*///)",
                Suggestion = "Add XML documentation comments for this public method",
                Languages = new[] { "csharp" },
                AppliesTo = new[] { "*.cs" }
            }
        };
    }

    public void Dispose()
    {
        _analysisSemaphore?.Dispose();
    }
}
