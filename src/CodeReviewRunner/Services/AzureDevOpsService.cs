using System.Net.Http.Headers;
using CodeReviewRunner.Models;
using CodeReviewRunner.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;

namespace CodeReviewRunner.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly CodeReviewOptions _options;

    public AzureDevOpsService(
        HttpClient httpClient,
        ILogger<AzureDevOpsService> logger,
        IOptions<CodeReviewOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        // Configure authentication
        var pat = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        if (!string.IsNullOrWhiteSpace(pat))
        {
            var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($":{pat}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        // Configure timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.AzureDevOps.TimeoutSeconds);
    }

    public async Task<List<(string path, string content)>> GetPullRequestChangedFilesAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching changed files for PR {PullRequestId} in repository {RepositoryId}",
                pullRequestId, repositoryId);

            // First get the repository name from the repository ID
            var repoName = await GetRepositoryNameAsync(organization, project, repositoryId, cancellationToken);
            if (string.IsNullOrEmpty(repoName))
            {
                _logger.LogError("Could not determine repository name from repository ID");
                return new List<(string path, string content)>();
            }

            _logger.LogInformation("Repository name: {RepositoryName}", repoName);

            // List pull requests to see what's available
            await ListPullRequestsAsync(organization, project, repositoryId, repoName, cancellationToken);

            // Try different URL formats and API versions
            var apiVersions = new[] { "7.0", "6.0", "5.1", "4.1" };
            var urls = new List<string>();

            foreach (var version in apiVersions)
            {
                urls.Add($"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repoName}/pullRequests/{pullRequestId}/changes?api-version={version}");
                urls.Add($"{organization.TrimEnd('/')}/_apis/git/repositories/{repoName}/pullRequests/{pullRequestId}/changes?api-version={version}");
                urls.Add($"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/changes?api-version={version}");
                urls.Add($"{organization.TrimEnd('/')}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/changes?api-version={version}");
            }

            foreach (var url in urls)
            {
                _logger.LogDebug("Trying: {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                _logger.LogDebug("Response Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Success with URL: {Url}", url);
                    return await ProcessPullRequestChanges(response, cancellationToken);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogDebug("Failed with {Url}: {StatusCode} - {Error}", url, response.StatusCode, errorContent);
                }
            }

            _logger.LogError("All URL attempts failed for PR {PullRequestId}", pullRequestId);
            return new List<(string path, string content)>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pull request changed files");
            return new List<(string path, string content)>();
        }
    }

    public async Task<bool> TestRepositoryAccessAsync(
        string organization,
        string project,
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var urls = new[]
            {
                $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}?api-version={_options.AzureDevOps.ApiVersion}",
                $"{organization.TrimEnd('/')}/_apis/git/repositories/{repositoryId}?api-version={_options.AzureDevOps.ApiVersion}"
            };

            foreach (var url in urls)
            {
                _logger.LogDebug("Testing repository access: {Url}", url);

                var response = await _httpClient.GetAsync(url, cancellationToken);
                _logger.LogDebug("Repository access test - Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        _logger.LogInformation("Repository name: {RepositoryName}", nameProp.GetString());
                    }
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogDebug("Repository access failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception testing repository access");
            return false;
        }
    }

    public async Task PostCommentsAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        string repositoryPath,
        List<CodeIssue> issues,
        IEnumerable<string>? allowedFilePaths = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Notifications.EnableComments)
        {
            _logger.LogInformation("Comments posting is disabled");
            return;
        }

        var url = $"{organization}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/threads?api-version=6.0";

        var allowedSet = allowedFilePaths != null
            ? new HashSet<string>(allowedFilePaths.Select(p => NormalizeForCompare(repositoryPath, p)), StringComparer.OrdinalIgnoreCase)
            : null;

        var commentCount = 0;
        foreach (var issue in issues.Take(_options.Notifications.MaxCommentsPerFile))
        {
            var relativePath = Path.DirectorySeparatorChar == '/'
                ? Path.GetRelativePath(repositoryPath, issue.FilePath)
                : Path.GetRelativePath(repositoryPath, issue.FilePath).Replace('\\', '/');
            if (!relativePath.StartsWith('/'))
                relativePath = "/" + relativePath;

            if (allowedSet != null)
            {
                var normalizedIssuePath = NormalizeForCompare(repositoryPath, Path.Combine(repositoryPath, relativePath.TrimStart('/')));
                if (!allowedSet.Contains(normalizedIssuePath))
                {
                    _logger.LogDebug("Skip commenting on {RelativePath} (not in PR changed files)", relativePath);
                    continue;
                }
            }

            var body = new
            {
                comments = new[] {
                    new {
                        parentCommentId = 0,
                        content = $"{issue.Severity.ToUpper()}: {issue.Message} (rule {issue.RuleId})",
                        commentType = "text"
                    }
                },
                status = "active",
                threadContext = new
                {
                    filePath = relativePath,
                    rightFileStart = new { line = issue.Line, offset = 1 },
                    rightFileEnd = new { line = issue.Line, offset = 1 }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(body);
            var response = await _httpClient.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
            _logger.LogDebug("Post comment on {RelativePath} line {Line}: {StatusCode}", relativePath, issue.Line, response.StatusCode);
            commentCount++;
        }

        _logger.LogInformation("Posted {CommentCount} comments to Azure DevOps", commentCount);
    }

    public async Task PostSummaryAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        List<CodeIssue> issues,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Notifications.EnableSummary)
        {
            _logger.LogInformation("Summary posting is disabled");
            return;
        }

        var url = $"{organization}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/threads?api-version=6.0";

        var errorCount = issues.Count(i => i.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        var warnCount = issues.Count(i => i.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
        var byLang = issues
            .GroupBy(i => i.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "C#" :
                (i.FilePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                 i.FilePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                 i.FilePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                 i.FilePath.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ? "JS/TS" : "Other"))
            .Select(g => $"- {g.Key}: {g.Count()} issues");

        var content = $"[CodeReview Bot] Summary\n\n" +
                      $"- Errors: {errorCount}\n" +
                      $"- Warnings: {warnCount}\n" +
                      string.Join("\n", byLang.Take(10));

        var body = new
        {
            comments = new[] {
                new { parentCommentId = 0, content = content, commentType = "text" }
            },
            status = "active"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var response = await _httpClient.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
        _logger.LogInformation("Posted summary: {StatusCode}", response.StatusCode);
    }

    private async Task<string> GetRepositoryNameAsync(string org, string project, string repoId, CancellationToken cancellationToken)
    {
        try
        {
            var urls = new[]
            {
                $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}?api-version={_options.AzureDevOps.ApiVersion}",
                $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}?api-version={_options.AzureDevOps.ApiVersion}"
            };

            foreach (var url in urls)
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("name", out var nameProp))
                    {
                        return nameProp.GetString() ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting repository name");
            return string.Empty;
        }
    }

    private async Task ListPullRequestsAsync(string org, string project, string repoId, string repoName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Listing pull requests for repository {RepositoryName}", repoName);
            var urls = new[]
            {
                $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoId}/pullRequests?api-version={_options.AzureDevOps.ApiVersion}",
                $"{org.TrimEnd('/')}/_apis/git/repositories/{repoId}/pullRequests?api-version={_options.AzureDevOps.ApiVersion}",
                $"{org.TrimEnd('/')}/{project}/_apis/git/repositories/{repoName}/pullRequests?api-version={_options.AzureDevOps.ApiVersion}",
                $"{org.TrimEnd('/')}/_apis/git/repositories/{repoName}/pullRequests?api-version={_options.AzureDevOps.ApiVersion}"
            };

            foreach (var url in urls)
            {
                _logger.LogDebug("Trying: {Url}", url);
                var response = await _httpClient.GetAsync(url, cancellationToken);
                _logger.LogDebug("Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("value", out var valueProp))
                    {
                        _logger.LogInformation("Pull requests found:");
                        foreach (var pr in valueProp.EnumerateArray())
                        {
                            if (pr.TryGetProperty("pullRequestId", out var idProp) &&
                                pr.TryGetProperty("title", out var titleProp))
                            {
                                var id = idProp.GetInt32();
                                var title = titleProp.GetString();
                                _logger.LogInformation("  - PR #{Id}: {Title}", id, title);
                            }
                        }
                    }
                    return;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogDebug("Error: {Error}", error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception listing pull requests");
        }
    }

    private async Task<List<(string path, string content)>> ProcessPullRequestChanges(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        var result = new List<(string path, string content)>();

        if (doc.RootElement.TryGetProperty("changes", out var changes))
        {
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("item", out var item)) continue;
                if (!item.TryGetProperty("path", out var pathProp)) continue;

                var path = pathProp.GetString();
                if (string.IsNullOrEmpty(path)) continue;

                // Check if it's a file we want to analyze
                var isAnalyzableFile = _options.Analysis.SupportedFileExtensions.Any(ext =>
                    path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                if (!isAnalyzableFile) continue;

                // Skip deleted files
                if (change.TryGetProperty("changeType", out var changeTypeEl))
                {
                    var changeType = changeTypeEl.GetString();
                    if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                _logger.LogDebug("Found changed file: {Path}", path);

                // For now, just add the path without content to test the API
                result.Add((path, ""));
                _logger.LogDebug("Added file to analysis list: {Path}", path);
            }
        }

        _logger.LogInformation("Total analyzable files found: {Count}", result.Count);
        return result;
    }

    private static string NormalizeForCompare(string repoPath, string fullPath)
    {
        var normalized = fullPath;
        if (Path.DirectorySeparatorChar == '\\')
            normalized = normalized.Replace('/', '\\');
        else
            normalized = normalized.Replace('\\', '/');
        var rel = Path.GetRelativePath(repoPath, normalized);
        return rel.TrimStart('/', '\\');
    }
}
