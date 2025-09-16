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

    // Removed JS/TS analysis per request

    public async Task<List<CodeIssue>> AnalyzeFilesAsync(
        Newtonsoft.Json.Linq.JObject rules,
        IEnumerable<(string path, string content)> files,
        CancellationToken cancellationToken = default)
    {
        var allIssues = new List<CodeIssue>();

        // Analyze C# files only
        var csharpIssues = await AnalyzeCSharpFilesAsync(rules, files, cancellationToken);
        allIssues.AddRange(csharpIssues);

        _logger.LogInformation("Total analysis completed: {TotalIssues} issues found", allIssues.Count);
        return allIssues;
    }
}
