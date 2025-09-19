using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;

namespace CodeReviewRunner.Services;

public class CodeReviewService : ICodeReviewService
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IAnalysisService _analysisService;
    private readonly IRulesService _rulesService;
    private readonly ILogger<CodeReviewService> _logger;
    private readonly CodeReviewOptions _options;

    public CodeReviewService(
        IAzureDevOpsService azureDevOpsService,
        IAnalysisService analysisService,
        IRulesService rulesService,
        ILogger<CodeReviewService> logger,
        IOptions<CodeReviewOptions> options)
    {
        _azureDevOpsService = azureDevOpsService;
        _analysisService = analysisService;
        _rulesService = rulesService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<CodeReviewResult> AnalyzePullRequestAsync(
        string rulesUrl,
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new CodeReviewResult();

        try
        {
            _logger.LogInformation("Starting pull request analysis for PR {PullRequestId} in repository {RepositoryId}",
                pullRequestId, repositoryId);

            // Test repository access first
            var hasAccess = await _azureDevOpsService.TestRepositoryAccessAsync(
                organization, project, repositoryId, cancellationToken);

            if (!hasAccess)
            {
                result.Errors.Add("Failed to access repository. Check credentials and repository ID.");
                result.Success = false;
                return result;
            }

            // Fetch rules JSON
            var ruleFetcher = new RuleFetcher();
            var rulesJson = await ruleFetcher.FetchAsync(string.IsNullOrWhiteSpace(rulesUrl) ? "coding-standards.sample.json" : rulesUrl);

            // Get changed files
            var changedFiles = await _azureDevOpsService.GetPullRequestChangedFilesAsync(
                organization, project, repositoryId, pullRequestId, cancellationToken);

            if (!changedFiles.Any())
            {
                result.Warnings.Add("No changed files detected in pull request.");
                result.Success = true;
                return result;
            }

            result.FilesAnalyzed = changedFiles.Count;

            // Filter files by supported extensions
            var supportedFiles = changedFiles.Where(f =>
                _options.Analysis.SupportedFileExtensions.Any(ext =>
                    f.path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))).ToList();

            if (!supportedFiles.Any())
            {
                result.Warnings.Add("No supported file types found for analysis.");
                result.Success = true;
                return result;
            }

            // Populate repo-changed paths to restrict commenting
            result.RepoChangedPaths = supportedFiles.Select(f => f.path.Replace('\\', '/').TrimStart('/')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Analyze files
            result.Issues = await _analysisService.AnalyzeFilesAsync(rulesJson, supportedFiles, cancellationToken);

            result.Success = true;
            _logger.LogInformation("Analysis completed. Found {IssueCount} issues in {FileCount} files",
                result.TotalIssues, result.FilesAnalyzed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Analysis was cancelled");
            result.Errors.Add("Analysis was cancelled");
            result.Success = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pull request analysis");
            result.Errors.Add($"Analysis failed: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<CodeReviewResult> AnalyzeLocalFilesAsync(
        string rulesUrl,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new CodeReviewResult();

        try
        {
            _logger.LogInformation("Starting local file analysis for {FileCount} files", filePaths.Count());

            var files = new List<(string path, string content)>();

            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    files.Add((filePath, content));
                }
                else
                {
                    result.Warnings.Add($"File not found: {filePath}");
                }
            }

            if (!files.Any())
            {
                result.Errors.Add("No valid files found for analysis.");
                result.Success = false;
                return result;
            }

            result.FilesAnalyzed = files.Count;

            // Analyze files with provided rules
            var ruleFetcher = new RuleFetcher();
            var rulesJson = await ruleFetcher.FetchAsync(string.IsNullOrWhiteSpace(rulesUrl) ? "coding-standards.sample.json" : rulesUrl);
            result.Issues = await _analysisService.AnalyzeFilesAsync(rulesJson, files, cancellationToken);

            result.Success = true;
            _logger.LogInformation("Local analysis completed. Found {IssueCount} issues in {FileCount} files",
                result.TotalIssues, result.FilesAnalyzed);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Local analysis was cancelled");
            result.Errors.Add("Analysis was cancelled");
            result.Success = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during local file analysis");
            result.Errors.Add($"Analysis failed: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }
}
