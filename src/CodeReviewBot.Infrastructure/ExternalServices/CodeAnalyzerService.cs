using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class CodeAnalyzerService : ICodeAnalyzer
{
    private readonly ILogger<CodeAnalyzerService> _logger;
    private List<CodingRule> _cachingRules = new();

    public CodeAnalyzerService(ILogger<CodeAnalyzerService> logger)
    {
        _logger = logger;
    }

    public async Task<List<CodeIssue>> AnalyzeFileAsync(FileChange fileChange)
    {
        var issues = new List<CodeIssue>();

        if (string.IsNullOrEmpty(fileChange.Content))
        {
            _logger.LogWarning("No content found for file {FilePath}", fileChange.Path);
            return issues;
        }

        try
        {
            _logger.LogInformation("Analyzing file {FilePath}", fileChange.Path);

            var rules = await LoadCodingRulesAsync();
            var lines = fileChange.Content.Split('\n');

            foreach (var rule in rules)
            {
                var ruleIssues = await AnalyzeRuleAsync(fileChange, lines, rule);
                issues.AddRange(ruleIssues);
            }

            _logger.LogInformation("Found {IssueCount} issues in file {FilePath}", issues.Count, fileChange.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", fileChange.Path);
        }

        return issues;
    }

    public async Task<List<CodingRule>> LoadCodingRulesAsync()
    {
        if (_cachingRules.Any())
        {
            return _cachingRules;
        }

        try
        {
            var rulesJson = await File.ReadAllTextAsync("coding-standards.json");
            var rules = JsonSerializer.Deserialize<List<CodingRule>>(rulesJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cachingRules = rules ?? new List<CodingRule>();
            _logger.LogInformation("Loaded {RuleCount} coding rules", _cachingRules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load coding standards");
            _cachingRules = GetDefaultRules();
        }

        return _cachingRules;
    }

    private async Task<List<CodeIssue>> AnalyzeRuleAsync(FileChange fileChange, string[] lines, CodingRule rule)
    {
        var issues = new List<CodeIssue>();

        try
        {
            if (string.IsNullOrEmpty(rule.Pattern))
                return issues;

            var regex = new Regex(rule.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                var matches = regex.Matches(lines[i]);
                foreach (Match match in matches)
                {
                    issues.Add(new CodeIssue
                    {
                        RuleId = rule.Id,
                        Severity = rule.Severity,
                        Message = rule.Message,
                        Suggestion = rule.Suggestion,
                        FilePath = fileChange.Path,
                        LineNumber = i + 1
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing rule {RuleId} for file {FilePath}", rule.Id, fileChange.Path);
        }

        return issues;
    }

    private List<CodingRule> GetDefaultRules()
    {
        return new List<CodingRule>
        {
            new CodingRule
            {
                Id = "CLASS_NAMING",
                Severity = "Warning",
                Message = "Class names should be PascalCase",
                Pattern = @"class\s+[a-z]",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "METHOD_PARAMETER_COUNT",
                Severity = "Warning",
                Message = "Method has too many parameters (more than 5)",
                Pattern = @"\w+\s+\w+\s*\([^)]*,[^)]*,[^)]*,[^)]*,[^)]*,[^)]*",
                Languages = new[] { "csharp" }
            }
        };
    }
}
