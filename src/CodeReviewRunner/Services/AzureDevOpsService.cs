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
            _logger.LogInformation("Repository ID: {RepositoryId}", repositoryId);
            _logger.LogInformation("Pull Request ID: {PullRequestId}", pullRequestId);

            // Check if the pull request ID is valid
            if (!int.TryParse(pullRequestId, out var prId))
            {
                _logger.LogError("Invalid pull request ID format: {PullRequestId}. Expected a numeric value.", pullRequestId);
                return new List<(string path, string content)>();
            }

            // List pull requests to see what's available
            await ListPullRequestsAsync(organization, project, repositoryId, repoName, cancellationToken);

            // First, get the pull request details to understand its structure
            var prDetails = await GetPullRequestDetailsAsync(organization, project, repositoryId, pullRequestId, cancellationToken);
            if (prDetails == null)
            {
                _logger.LogError("Could not fetch pull request details for PR {PullRequestId}", pullRequestId);
                return new List<(string path, string content)>();
            }

            // Now try to get the changes using different approaches
            var apiVersions = new[] { "7.0", "6.0", "5.1", "4.1" };
            var urls = new List<string>();

            foreach (var version in apiVersions)
            {
                // Only use commits endpoint; the changes endpoint frequently 404s depending on permissions/config
                urls.Add($"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/commits?api-version={version}");
            }

            foreach (var url in urls)
            {
                _logger.LogInformation("Trying: {Url}", url);

                try
                {
                    var response = await _httpClient.GetAsync(url, cancellationToken);
                    _logger.LogInformation("Response Status: {StatusCode}", response.StatusCode);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("✅ Success with URL: {Url}", url);

                        // Only handling commits flow
                        if (url.Contains("/commits"))
                        {
                            return await ProcessPullRequestCommits(organization, project, repositoryId, response, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("Unknown response type for URL: {Url}", url);
                            return new List<(string path, string content)>();
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                        // Check if we got HTML instead of JSON (common with 404s)
                        if (errorContent.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("❌ Failed with {Url}: {StatusCode} - Received HTML response (likely 404 page)", url, response.StatusCode);
                        }
                        else
                        {
                            _logger.LogWarning("❌ Failed with {Url}: {StatusCode} - {Error}", url, response.StatusCode, errorContent.Length > 500 ? errorContent.Substring(0, 500) + "..." : errorContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Exception calling {Url}", url);
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
                                var status = "Unknown";
                                if (pr.TryGetProperty("status", out var statusProp))
                                {
                                    status = statusProp.GetString() ?? "Unknown";
                                }
                                _logger.LogInformation("  - PR #{Id}: {Title} (Status: {Status})", id, title, status);
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

    private async Task<List<(string path, string content)>> ProcessPullRequestCommits(
        string organization,
        string project,
        string repositoryId,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var result = new List<(string path, string content)>();

            if (doc.RootElement.TryGetProperty("value", out var commits))
            {
                _logger.LogInformation("Found {CommitCount} commits in pull request", commits.GetArrayLength());

                // Get the first and last commit to determine the diff range
                var commitArray = commits.EnumerateArray().ToList();
                if (commitArray.Count > 0)
                {
                    var firstCommit = commitArray.First();
                    var lastCommit = commitArray.Last();

                    if (firstCommit.TryGetProperty("commitId", out var firstCommitId) &&
                        lastCommit.TryGetProperty("commitId", out var lastCommitId))
                    {
                        _logger.LogInformation("Getting changes between commits: {FirstCommit} -> {LastCommit}",
                            firstCommitId.GetString(), lastCommitId.GetString());

                        // Try to get the diff between commits
                        var changes = await GetChangesBetweenCommits(organization, project, repositoryId, firstCommitId.GetString()!, lastCommitId.GetString()!, cancellationToken);
                        result.AddRange(changes);
                    }
                }
            }

            _logger.LogInformation("Processed {FileCount} changed files from commits", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pull request commits");
            return new List<(string path, string content)>();
        }
    }

    private async Task<List<(string path, string content)>> GetChangesBetweenCommits(
        string organization,
        string project,
        string repositoryId,
        string fromCommit,
        string toCommit,
        CancellationToken cancellationToken)
    {
        try
        {
            // Page through diff to collect all results
            var result = new List<(string path, string content)>();
            var changedPaths = new List<string>();
            const int pageSize = 200;
            var skip = 0;
            while (true)
            {
                var pagedUrl = $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/diffs/commits?baseVersion={fromCommit}&baseVersionType=commit&targetVersion={toCommit}&targetVersionType=commit&$top={pageSize}&$skip={skip}&api-version={_options.AzureDevOps.ApiVersion}";
                _logger.LogInformation("Getting diff between commits: {DiffUrl}", pagedUrl);
                var response = await _httpClient.GetAsync(pagedUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get diff page: {StatusCode}", response.StatusCode);
                    break;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var addedThisPage = 0;

                if (doc.RootElement.TryGetProperty("changes", out var changesEl))
                {
                    foreach (var change in changesEl.EnumerateArray())
                    {
                        // Skip deletes
                        if (change.TryGetProperty("changeType", out var changeTypeEl))
                        {
                            var changeType = changeTypeEl.GetString();
                            if (string.Equals(changeType, "delete", StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        var candidates = new List<string>();
                        if (change.TryGetProperty("item", out var item))
                        {
                            if (item.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == System.Text.Json.JsonValueKind.String)
                                candidates.Add(pathProp.GetString() ?? string.Empty);
                        }
                        if (change.TryGetProperty("newItem", out var newItem))
                        {
                            if (newItem.TryGetProperty("path", out var newPathProp) && newPathProp.ValueKind == System.Text.Json.JsonValueKind.String)
                                candidates.Add(newPathProp.GetString() ?? string.Empty);
                        }
                        if (change.TryGetProperty("oldItem", out var oldItem))
                        {
                            if (oldItem.TryGetProperty("path", out var oldPathProp) && oldPathProp.ValueKind == System.Text.Json.JsonValueKind.String)
                                candidates.Add(oldPathProp.GetString() ?? string.Empty);
                        }
                        if (change.TryGetProperty("newPath", out var newPathEl) && newPathEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            candidates.Add(newPathEl.GetString() ?? string.Empty);
                        if (change.TryGetProperty("originalPath", out var originalPathEl) && originalPathEl.ValueKind == System.Text.Json.JsonValueKind.String)
                            candidates.Add(originalPathEl.GetString() ?? string.Empty);

                        foreach (var candidate in candidates)
                        {
                            if (!string.IsNullOrWhiteSpace(candidate))
                            {
                                changedPaths.Add(candidate);
                                addedThisPage++;
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Unexpected diff schema. Sample: {Sample}", json.Length > 500 ? json.Substring(0, 500) + "..." : json);
                    break;
                }

                if (addedThisPage < pageSize)
                {
                    break;
                }
                skip += pageSize;
            }

            _logger.LogInformation("Found changes in diff");

            foreach (var path in changedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fileUrl = $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/items?path={Uri.EscapeDataString(path)}&versionDescriptor.version={toCommit}&versionDescriptor.versionType=commit&includeContent=true&resolveLfs=true&api-version={_options.AzureDevOps.ApiVersion}";
                try
                {
                    var fileResponse = await _httpClient.GetAsync(fileUrl, cancellationToken);
                    if (fileResponse.IsSuccessStatusCode)
                    {
                        var contentType = fileResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
                        var raw = await fileResponse.Content.ReadAsStringAsync(cancellationToken);

                        string? content = null;
                        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                        {
                            using var itemDoc = System.Text.Json.JsonDocument.Parse(raw);
                            var root = itemDoc.RootElement;
                            if (root.TryGetProperty("isBinary", out var isBinaryEl) && isBinaryEl.GetBoolean())
                            {
                                _logger.LogDebug("Skipping binary file {Path}", path);
                                continue;
                            }
                            if (root.TryGetProperty("content", out var contentEl))
                            {
                                if (root.TryGetProperty("isBase64Encoded", out var b64El) && b64El.ValueKind == System.Text.Json.JsonValueKind.True)
                                {
                                    var bytes = Convert.FromBase64String(contentEl.GetString() ?? string.Empty);
                                    content = System.Text.Encoding.UTF8.GetString(bytes);
                                }
                                else
                                {
                                    content = contentEl.GetString();
                                }
                            }
                        }
                        else
                        {
                            content = raw;
                        }

                        if (content != null)
                        {
                            result.Add((path, content));
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Could not fetch content for {Path}: {Status}", path, fileResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Exception fetching content for {Path}", path);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting changes between commits");
            return new List<(string path, string content)>();
        }
    }

    private string GetSampleCSharpCode()
    {
        return @"using System;
using System.Collections.Generic;

public class UserController
{
    public void MethodWithVeryLongNameThatExceedsTheMaximumAllowedLengthForCodingStandards()
    {
        // This method name is too long and should trigger a warning
        var unusedVariable = ""This variable is not used"";
        Console.WriteLine(""Hello World"");
    }
    
    public void GoodMethod()
    {
        // This method name is fine
        Console.WriteLine(""Hello World"");
    }
}";
    }

    private string GetSampleReactCode()
    {
        return @"import React from 'react';

const ComponentWithVeryLongNameThatExceedsTheMaximumAllowedLength = () => {
    // This component name is too long
    const unusedVariable = 'This variable is not used';
    return <div>Hello World</div>;
};

const GoodComponent = () => {
    // This component name is fine
    return <div>Hello World</div>;
};

export default GoodComponent;";
    }

    private string GetSampleJavaScriptCode()
    {
        return @"function functionWithVeryLongNameThatExceedsTheMaximumAllowedLength() {
    // This function name is too long
    const unusedVariable = 'This variable is not used';
    console.log('Hello World');
}

function goodFunction() {
    // This function name is fine
    console.log('Hello World');
}";
    }

    private async Task<object?> GetPullRequestDetailsAsync(string organization, string project, string repositoryId, string pullRequestId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}?api-version=7.0";
            _logger.LogInformation("Fetching pull request details: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = System.Text.Json.JsonDocument.Parse(content);

                _logger.LogInformation("✅ Successfully fetched pull request details");
                _logger.LogInformation("PR Status: {Status}", doc.RootElement.GetProperty("status").GetString());
                _logger.LogInformation("Source Branch: {SourceBranch}", doc.RootElement.GetProperty("sourceRefName").GetString());
                _logger.LogInformation("Target Branch: {TargetBranch}", doc.RootElement.GetProperty("targetRefName").GetString());

                return doc.RootElement;
            }
            else
            {
                _logger.LogError("Failed to fetch pull request details: {StatusCode}", response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception fetching pull request details");
            return null;
        }
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
