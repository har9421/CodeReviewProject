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
            _logger.LogInformation("Analyzing file {FilePath} - AnalyzeOnlyChangedLines: {AnalyzeOnlyChangedLines}, ChangedLinesCount: {ChangedLinesCount}",
                fileChange.Path, fileChange.AnalyzeOnlyChangedLines, fileChange.ChangedLines.Count);

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

            // If analyzing only changed lines and there are no changed lines, skip analysis entirely
            if (fileChange.AnalyzeOnlyChangedLines && !fileChange.ChangedLines.Any())
            {
                _logger.LogInformation("Skipping analysis for file {FilePath} - no changed lines detected", fileChange.Path);
                return issues;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                // If analyzing only changed lines, skip lines that weren't changed
                if (fileChange.AnalyzeOnlyChangedLines && fileChange.ChangedLines.Any() && !fileChange.ChangedLines.Contains(i + 1))
                {
                    continue;
                }

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
                Message = "Class names should follow PascalCase convention (first letter uppercase)",
                Pattern = @"^\s*(?:public\s+|private\s+|protected\s+|internal\s+)?class\s+[a-z][a-zA-Z0-9_]*(?:\s*\{|\s*//|\s*$)",
                Suggestion = "Consider renaming the class to start with an uppercase letter, e.g., 'MyClass' instead of 'myClass'",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "METHOD_PARAMETER_COUNT",
                Severity = "Warning",
                Message = "Method has too many parameters (more than 5)",
                Pattern = @"\w+\s+\w+\s*\([^)]*,[^)]*,[^)]*,[^)]*,[^)]*,[^)]*",
                Suggestion = "Consider refactoring to use fewer parameters. Use objects or data structures to group related parameters.",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "CONSOLE_WRITELINE_USAGE",
                Severity = "Warning",
                Message = "Console.WriteLine should not be used in production code",
                Pattern = @"Console\.WriteLine\s*\(",
                Suggestion = "Use proper logging framework (ILogger, Serilog, etc.) instead of Console.WriteLine for production code",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "UNUSED_VARIABLE",
                Severity = "Warning",
                Message = "Variable appears to be declared but not used",
                Pattern = @"\b(int|string|bool|var|object|double|float|decimal|char|byte|short|long|DateTime|Guid)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*[=;]",
                Suggestion = "Remove unused variables or use them in your code. Consider using underscore prefix for intentionally unused variables.",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "MAGIC_NUMBERS",
                Severity = "Info",
                Message = "Magic numbers should be replaced with named constants",
                Pattern = @"\b\d{2,}\b",
                Suggestion = "Replace magic numbers with meaningful named constants or configuration values",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "EMPTY_CATCH_BLOCK",
                Severity = "Warning",
                Message = "Empty catch blocks should be avoided",
                Pattern = @"catch\s*\([^)]*\)\s*\{\s*\}|catch\s*\{\s*\}",
                Suggestion = "Handle exceptions appropriately or at least log them. Avoid empty catch blocks.",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "STRING_CONCATENATION",
                Severity = "Info",
                Message = "Consider using string interpolation or StringBuilder for string concatenation",
                Pattern = @"[^""]\+\s*""[^""]*""|\""[^""]*""\s*\+\s*[^""]",
                Suggestion = "Use string interpolation ($\"text {variable}\") or StringBuilder for better performance with multiple concatenations",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "LONG_METHOD",
                Severity = "Warning",
                Message = "Method appears to be too long (more than 20 lines)",
                Pattern = @"\w+\s+\w+\s*\([^)]*\)\s*\{(?:[^{}]*(?:\{[^{}]*\}[^{}]*)*){20,}",
                Suggestion = "Consider breaking this method into smaller, more focused methods for better readability and maintainability",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "METHOD_NAMING",
                Severity = "Warning",
                Message = "Method names should follow PascalCase convention (first letter uppercase)",
                Pattern = @"^\s*(?:public\s+|private\s+|protected\s+|internal\s+)?\w+\s+[a-z][a-zA-Z0-9_]*\s*\(",
                Suggestion = "Consider renaming the method to start with an uppercase letter, e.g., 'GetUser' instead of 'getUser'",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "FIELD_NAMING",
                Severity = "Warning",
                Message = "Private fields should follow camelCase convention or use underscore prefix",
                Pattern = @"^\s*private\s+\w+\s+[A-Z][a-zA-Z0-9_]*\s*[=;]",
                Suggestion = "Consider using camelCase (e.g., 'userName') or underscore prefix (e.g., '_userName') for private fields",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "HARDCODED_STRINGS",
                Severity = "Info",
                Message = "Consider using constants or configuration for hardcoded strings",
                Pattern = @"\""[A-Z][a-zA-Z0-9\s]{10,}\""",
                Suggestion = "Move hardcoded strings to constants, configuration files, or resource files for better maintainability",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "USING_DIRECTIVES",
                Severity = "Info",
                Message = "Consider organizing using directives (System first, then third-party, then local)",
                Pattern = @"^using\s+(?!System)[A-Z]",
                Suggestion = "Organize using statements: System namespaces first, then third-party libraries, then your own namespaces",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "COMMENTED_CODE",
                Severity = "Warning",
                Message = "Commented code should be removed",
                Pattern = @"^\s*//\s*(?:public\s+\w+|private\s+\w+|protected\s+\w+|internal\s+\w+|class\s+\w+|interface\s+\w+|enum\s+\w+|struct\s+\w+|namespace\s+\w+|using\s+\w+|if\s*\(|for\s*\(|while\s*\(|foreach\s*\(|switch\s*\(|try\s*\{|catch\s*\(|finally\s*\{|return\s+[^;]*;|var\s+\w+\s*=|int\s+\w+\s*[=;]|string\s+\w+\s*[=;]|bool\s+\w+\s*[=;]|object\s+\w+\s*[=;]|Console\.\w+\s*\()",
                Suggestion = "Remove commented code blocks. Use version control to track code history instead.",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "TODO_COMMENTS",
                Severity = "Info",
                Message = "TODO comments should be tracked and resolved",
                Pattern = @"//\s*TODO\b",
                Suggestion = "Consider resolving TODO comments or tracking them in your project management system",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "FIXME_COMMENTS",
                Severity = "Warning",
                Message = "FIXME comments indicate code that needs attention",
                Pattern = @"//\s*FIXME\b",
                Suggestion = "Address FIXME comments as they indicate code that needs to be fixed",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "DEPRECATED_ATTRIBUTES",
                Severity = "Warning",
                Message = "Code marked as deprecated should be replaced",
                Pattern = @"\[Obsolete\(",
                Suggestion = "Replace deprecated code with the recommended alternative",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "ASYNC_VOID",
                Severity = "Error",
                Message = "Avoid async void methods (except event handlers)",
                Pattern = @"^\s*(?:public\s+|private\s+|protected\s+|internal\s+)?async\s+void\s+",
                Suggestion = "Use async Task instead of async void, except for event handlers",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "SUPPRESS_WARNINGS",
                Severity = "Info",
                Message = "Suppressing warnings should be justified",
                Pattern = @"\[SuppressMessage\(",
                Suggestion = "Ensure suppressed warnings are justified and documented",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "CATCH_EXCEPTION",
                Severity = "Warning",
                Message = "Avoid catching generic Exception type",
                Pattern = @"catch\s*\(\s*Exception\s+",
                Suggestion = "Catch specific exception types instead of the generic Exception class",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "THROW_EXCEPTION",
                Severity = "Warning",
                Message = "Avoid throwing generic Exception type",
                Pattern = @"throw\s+new\s+Exception\s*\(",
                Suggestion = "Throw specific exception types instead of the generic Exception class",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "GOTO_STATEMENT",
                Severity = "Error",
                Message = "Goto statements should be avoided",
                Pattern = @"\bgoto\b",
                Suggestion = "Refactor code to avoid goto statements. Use proper control flow structures instead.",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "RECURSION_DEPTH",
                Severity = "Warning",
                Message = "Deep recursion may cause stack overflow",
                Pattern = @"\w+\s+\w+\s*\([^)]*\)\s*\{\s*(?:[^{}]*(?:\{[^{}]*\}[^{}]*)*){5,}",
                Suggestion = "Consider using iterative approaches for deep recursion to avoid stack overflow",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "MULTIPLE_RETURNS",
                Severity = "Info",
                Message = "Multiple return statements may reduce readability",
                Pattern = @"return\s+.*;\s*(?:[^}]*\n){0,10}.*return\s+.*;",
                Suggestion = "Consider consolidating multiple return statements for better readability",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "SWITCH_WITHOUT_DEFAULT",
                Severity = "Warning",
                Message = "Switch statements should have a default case",
                Pattern = @"switch\s*\([^)]*\)\s*\{(?:[^{}]*(?:\{[^{}]*\}[^{}]*)*)*\}(?!\s*default)",
                Suggestion = "Add a default case to handle unexpected values",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "DISPOSE_PATTERN",
                Severity = "Warning",
                Message = "Classes with disposable resources should implement IDisposable",
                Pattern = @"class\s+\w+.*(?:FileStream|Stream|Connection|HttpClient|Timer|CancellationTokenSource)",
                Suggestion = "Implement IDisposable pattern for classes that use disposable resources",
                Languages = new[] { "csharp" }
            },
            new CodingRule
            {
                Id = "STATIC_CONSTRUCTOR",
                Severity = "Info",
                Message = "Static constructors should be simple and not throw exceptions",
                Pattern = @"static\s+\w+\(\)\s*\{",
                Suggestion = "Keep static constructors simple and avoid throwing exceptions",
                Languages = new[] { "csharp" }
            }
        };
    }
}
