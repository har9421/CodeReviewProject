using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;

namespace CodeReviewRunner.Services;

public class AnalysisService : IAnalysisService
{
    private readonly CSharpAnalyzer _csharpAnalyzer;
    private readonly ReactAnalyzer _reactAnalyzer;
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
        _reactAnalyzer = new ReactAnalyzer();
    }

    public async Task<List<CodeIssue>> AnalyzeCSharpFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        var csharpFiles = files.Where(f => f.path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();

        if (!csharpFiles.Any())
        {
            return new List<CodeIssue>();
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
        return issues;
    }

    public async Task<List<CodeIssue>> AnalyzeJavaScriptFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        var jsFiles = files.Where(f =>
            f.path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)).ToList();

        if (!jsFiles.Any())
        {
            return new List<CodeIssue>();
        }

        _logger.LogInformation("Analyzing {FileCount} JavaScript/TypeScript files", jsFiles.Count);

        var issues = _reactAnalyzer.AnalyzeFromContent(rules, jsFiles);

        // Enhance issues with additional metadata
        foreach (var issue in issues)
        {
            issue.Analyzer = "ReactAnalyzer";
            issue.Category = "Code Quality";

            if (issue.FilePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                issue.FilePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
            {
                issue.Tags.Add("typescript");
            }
            else
            {
                issue.Tags.Add("javascript");
            }

            issue.Tags.Add("eslint");
        }

        _logger.LogInformation("Found {IssueCount} JavaScript/TypeScript issues", issues.Count);
        return issues;
    }

    public async Task<List<CodeIssue>> AnalyzeFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        var allIssues = new List<CodeIssue>();

        // Analyze C# files
        var csharpIssues = await AnalyzeCSharpFilesAsync(rules, files, cancellationToken);
        allIssues.AddRange(csharpIssues);

        // Analyze JavaScript/TypeScript files
        var jsIssues = await AnalyzeJavaScriptFilesAsync(rules, files, cancellationToken);
        allIssues.AddRange(jsIssues);

        _logger.LogInformation("Total analysis completed: {TotalIssues} issues found", allIssues.Count);
        return allIssues;
    }
}
