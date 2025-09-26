using CodeReviewBot.Configuration;
using CodeReviewBot.Interfaces;
using CodeReviewBot.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeReviewBot.Services;

public class CodeAnalyzerService : ICodeAnalyzerService
{
    private readonly ILogger<CodeAnalyzerService> _logger;
    private readonly BotOptions _botOptions;
    private readonly HttpClient _httpClient;
    private List<CodingRule> _cachingRules = new();

    public CodeAnalyzerService(
        ILogger<CodeAnalyzerService> logger,
        IOptions<BotOptions> botOptions,
        HttpClient httpClient)
    {
        _logger = logger;
        _botOptions = botOptions.Value;
        _httpClient = httpClient;
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

            var rules = await GetCodingRulesAsync();
            var lines = fileChange.Content.Split('\n');

            foreach (var rule in rules)
            {
                var ruleIssues = await AnalyzeRuleAsync(fileChange, lines, rule);
                issues.AddRange(ruleIssues);
            }

            _logger.LogInformation("Found {IssueCount} issues in file {FilePath}", issues.Count, fileChange.Path);
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", fileChange.Path);
            return issues;
        }
    }

    private async Task<List<CodeIssue>> AnalyzeRuleAsync(FileChange fileChange, string[] lines, CodingRule rule)
    {
        var issues = new List<CodeIssue>();

        try
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;

                // Check if rule applies to this line
                if (!ShouldApplyRule(line, rule))
                    continue;

                // Apply regex pattern matching
                if (!string.IsNullOrEmpty(rule.Pattern))
                {
                    var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase);
                    if (regex.IsMatch(line))
                    {
                        issues.Add(new CodeIssue
                        {
                            FilePath = fileChange.Path,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = rule.Message,
                            RuleId = rule.Id,
                            Suggestion = rule.Suggestion
                        });
                    }
                }
                // Apply line-based checks
                else
                {
                    var issue = await CheckLineBasedRule(line, lineNumber, fileChange.Path, rule);
                    if (issue != null)
                    {
                        issues.Add(issue);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying rule {RuleId} to file {FilePath}", rule.Id, fileChange.Path);
        }

        return issues;
    }

    private bool ShouldApplyRule(string line, CodingRule rule)
    {
        // Skip empty lines and comments for most rules
        if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("//"))
        {
            return rule.AppliesTo?.Contains("comments") == true;
        }

        // Check if rule applies to this type of code
        if (rule.AppliesTo != null && rule.AppliesTo.Any())
        {
            var lowerLine = line.ToLower();

            foreach (var applyTo in rule.AppliesTo)
            {
                switch (applyTo.ToLower())
                {
                    case "methods":
                        if (IsMethodDeclaration(line)) return true;
                        break;
                    case "classes":
                        if (IsClassDeclaration(line)) return true;
                        break;
                    case "properties":
                        if (IsPropertyDeclaration(line)) return true;
                        break;
                    case "fields":
                        if (IsFieldDeclaration(line)) return true;
                        break;
                    case "variables":
                        if (IsVariableDeclaration(line)) return true;
                        break;
                    case "async":
                        if (line.Contains("async") || line.Contains("await")) return true;
                        break;
                    case "exception":
                        if (line.Contains("throw") || line.Contains("catch") || line.Contains("Exception")) return true;
                        break;
                    case "all":
                    default:
                        return true;
                }
            }

            return false;
        }

        return true;
    }

    private async Task<CodeIssue?> CheckLineBasedRule(string line, int lineNumber, string filePath, CodingRule rule)
    {
        try
        {
            switch (rule.Id)
            {
                case "method-too-long":
                    if (IsMethodDeclaration(line))
                    {
                        // This would need more sophisticated analysis to count method lines
                        return new CodeIssue
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = "Consider breaking down long methods into smaller, more focused methods",
                            RuleId = rule.Id,
                            Suggestion = "Extract logic into private methods with descriptive names"
                        };
                    }
                    break;

                case "too-many-parameters":
                    if (IsMethodDeclaration(line))
                    {
                        var parameterCount = CountParameters(line);
                        if (parameterCount > 4)
                        {
                            return new CodeIssue
                            {
                                FilePath = filePath,
                                LineNumber = lineNumber,
                                Severity = rule.Severity,
                                Message = $"Method has {parameterCount} parameters. Consider using a parameter object.",
                                RuleId = rule.Id,
                                Suggestion = "Create a parameter object or use builder pattern"
                            };
                        }
                    }
                    break;

                case "nested-if-depth":
                    var nestingLevel = CountNestingLevel(line);
                    if (nestingLevel > 3)
                    {
                        return new CodeIssue
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = $"Deep nesting detected (level {nestingLevel}). Consider refactoring.",
                            RuleId = rule.Id,
                            Suggestion = "Extract nested logic into separate methods or use early returns"
                        };
                    }
                    break;

                case "string-concatenation":
                    if (line.Contains("+") && (line.Contains("\"") || line.Contains("'")))
                    {
                        return new CodeIssue
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = "String concatenation detected. Consider using StringBuilder or string interpolation.",
                            RuleId = rule.Id,
                            Suggestion = "Use StringBuilder for multiple concatenations or $\"...\" for interpolation"
                        };
                    }
                    break;

                case "hardcoded-connection-string":
                    if (line.Contains("connectionString") || line.Contains("ConnectionString"))
                    {
                        return new CodeIssue
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = "Hardcoded connection string detected.",
                            RuleId = rule.Id,
                            Suggestion = "Use configuration or environment variables for connection strings"
                        };
                    }
                    break;

                case "missing-xml-documentation":
                    if (IsPublicMethodOrClass(line) && !HasXmlDocumentation(line))
                    {
                        return new CodeIssue
                        {
                            FilePath = filePath,
                            LineNumber = lineNumber,
                            Severity = rule.Severity,
                            Message = "Public method/class missing XML documentation.",
                            RuleId = rule.Id,
                            Suggestion = "Add XML documentation comments above the declaration"
                        };
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking line-based rule {RuleId}", rule.Id);
        }

        return null;
    }

    private async Task<List<CodingRule>> GetCodingRulesAsync()
    {
        if (_cachingRules.Any() && _botOptions.Analysis.EnableCaching)
        {
            return _cachingRules;
        }

        try
        {
            var rulesUrl = _botOptions.DefaultRulesUrl;

            // If it's a local file, read from file system
            if (!rulesUrl.StartsWith("http"))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), rulesUrl);
                if (File.Exists(filePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(filePath);
                    var rules = JsonSerializer.Deserialize<List<CodingRule>>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    _cachingRules = rules ?? new List<CodingRule>();
                    _logger.LogInformation("Loaded {RuleCount} coding rules from local file", _cachingRules.Count);
                    return _cachingRules;
                }
            }
            else
            {
                // If it's a URL, fetch from web
                var response = await _httpClient.GetAsync(rulesUrl);
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync();
                var rules = JsonSerializer.Deserialize<List<CodingRule>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _cachingRules = rules ?? new List<CodingRule>();
                _logger.LogInformation("Loaded {RuleCount} coding rules from URL", _cachingRules.Count);
                return _cachingRules;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load coding rules, using default rules");
        }

        // Return default rules if loading fails
        _cachingRules = GetDefaultRules();
        return _cachingRules;
    }

    private List<CodingRule> GetDefaultRules()
    {
        return new List<CodingRule>
        {
            new() { Id = "method-naming", Severity = "Warning", Message = "Method names should be PascalCase", Pattern = @"public\s+\w+\s+[a-z]", AppliesTo = new[] { "methods" } },
            new() { Id = "class-naming", Severity = "Warning", Message = "Class names should be PascalCase", Pattern = @"class\s+[a-z]", AppliesTo = new[] { "classes" } },
            new() { Id = "string-concatenation", Severity = "Info", Message = "Consider using StringBuilder for multiple string concatenations", Pattern = @".*\+.*\+", AppliesTo = new[] { "all" } }
        };
    }

    // Helper methods for code analysis
    private static bool IsMethodDeclaration(string line) =>
        Regex.IsMatch(line, @"^\s*(public|private|protected|internal)\s+\w+\s+\w+\s*\(");

    private static bool IsClassDeclaration(string line) =>
        Regex.IsMatch(line, @"^\s*(public|private|protected|internal)?\s*class\s+\w+");

    private static bool IsPropertyDeclaration(string line) =>
        Regex.IsMatch(line, @"^\s*(public|private|protected|internal)\s+\w+\s+\w+\s*\{");

    private static bool IsFieldDeclaration(string line) =>
        Regex.IsMatch(line, @"^\s*(public|private|protected|internal)\s+\w+\s+\w+\s*[;=]");

    private static bool IsVariableDeclaration(string line) =>
        Regex.IsMatch(line, @"^\s*\w+\s+\w+\s*=");

    private static int CountParameters(string methodLine)
    {
        var match = Regex.Match(methodLine, @"\(([^)]*)\)");
        if (!match.Success) return 0;

        var parameters = match.Groups[1].Value;
        if (string.IsNullOrWhiteSpace(parameters)) return 0;

        return parameters.Split(',').Length;
    }

    private static int CountNestingLevel(string line)
    {
        var openBraces = line.Count(c => c == '{');
        var closeBraces = line.Count(c => c == '}');
        return Math.Max(0, openBraces - closeBraces);
    }

    private static bool IsPublicMethodOrClass(string line)
    {
        return Regex.IsMatch(line, @"^\s*public\s+(class|interface|struct|enum|\w+\s+\w+\s*\()");
    }

    private static bool HasXmlDocumentation(string line)
    {
        // This is a simplified check - in reality, you'd need to check the previous lines
        return line.Trim().StartsWith("///");
    }
}
