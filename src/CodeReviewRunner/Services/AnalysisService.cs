using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;

namespace CodeReviewRunner.Services;

public class AnalysisService : IAnalysisService
{
    private readonly CSharpAnalyzer _csharpAnalyzer;
    private readonly IRulesService _rulesService;
    private readonly ILogger<AnalysisService> _logger;
    private readonly CodeReviewOptions _options;

    public AnalysisService(
        IRulesService rulesService,
        ILogger<AnalysisService> logger,
        IOptions<CodeReviewOptions> options)
    {
        _rulesService = rulesService;
        _logger = logger;
        _options = options.Value;
        _csharpAnalyzer = new CSharpAnalyzer();
    }

    public Task<List<CodeIssue>> AnalyzeCSharpFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        // Ensure we have a minimum set of C# rules if none are provided in expected schema
        rules = EnsureCSharpDefaultRules(rules);
        var csharpFiles = files.Where(f => f.path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();

        if (!csharpFiles.Any())
        {
            return Task.FromResult(new List<CodeIssue>());
        }

        _logger.LogInformation("Analyzing {FileCount} C# files", csharpFiles.Count);

        var issues = _csharpAnalyzer.AnalyzeFromContent(rules, csharpFiles);

        // Enhance issues with additional metadata
        foreach (var issue in issues)
        {
            issue.Analyzer = "CSharpAnalyzer";
            issue.Category = "Code Quality";
            issue.Tags.Add("csharp");
            issue.Tags.Add("roslyn");
        }

        _logger.LogInformation("Found {IssueCount} C# issues", issues.Count);
        return Task.FromResult(issues);
    }

    // Removed JS/TS analysis per request

    public async Task<List<CodeIssue>> AnalyzeFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        // Ensure minimal C# rules if needed
        rules = EnsureCSharpDefaultRules(rules);
        var allIssues = new List<CodeIssue>();

        // Analyze C# files only
        var csharpIssues = await AnalyzeCSharpFilesAsync(rules, files, cancellationToken);
        allIssues.AddRange(csharpIssues);

        _logger.LogInformation("Total analysis completed: {TotalIssues} issues found", allIssues.Count);
        return allIssues;
    }

    private static Newtonsoft.Json.Linq.JObject EnsureCSharpDefaultRules(Newtonsoft.Json.Linq.JObject rules)
    {
        var hasCsharp = rules["csharp"]?["rules"] is Newtonsoft.Json.Linq.JArray csharpArr && csharpArr.Count > 0;
        var hasTop = rules["rules"] is Newtonsoft.Json.Linq.JArray topArr && topArr.Any(r =>
            (r["languages"] as Newtonsoft.Json.Linq.JArray)?.Any(l => string.Equals((string?)l, "csharp", StringComparison.OrdinalIgnoreCase)) == true);

        if (hasCsharp || hasTop)
            return rules;

        // Inject minimal defaults while preserving existing content
        var defaults = new Newtonsoft.Json.Linq.JArray
        {
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS026",
                ["type"] = "forbidden",
                ["pattern"] = "Console.WriteLine",
                ["message"] = "Avoid Console.WriteLine in production; use logging framework.",
                ["severity"] = "error",
                ["applies_to"] = "file"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS004",
                ["type"] = "style",
                ["applies_to"] = "property_declaration",
                ["message"] = "Property names should be in PascalCase.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS002",
                ["type"] = "style",
                ["applies_to"] = "method_declaration",
                ["message"] = "Method names should be in PascalCase.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS001",
                ["type"] = "style",
                ["applies_to"] = "type_declaration",
                ["message"] = "Type names should be in PascalCase.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS005",
                ["type"] = "style",
                ["applies_to"] = "field_declaration",
                ["message"] = "Constants should be in ALL_CAPS with underscores.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS007",
                ["type"] = "style",
                ["applies_to"] = "field_declaration",
                ["message"] = "Private fields should be in camelCase and prefixed with '_'.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS008",
                ["type"] = "style",
                ["applies_to"] = "method_declaration",
                ["message"] = "Async methods should end with 'Async' suffix.",
                ["severity"] = "warning"
            },
            new Newtonsoft.Json.Linq.JObject
            {
                ["id"] = "CS012",
                ["type"] = "style",
                ["applies_to"] = "parameter_declaration",
                ["message"] = "Parameters must be in camelCase.",
                ["severity"] = "warning"
            }
        };

        if (rules["csharp"] == null)
        {
            rules["csharp"] = new Newtonsoft.Json.Linq.JObject();
        }
        rules["csharp"]!["rules"] = defaults;
        return rules;
    }
}
