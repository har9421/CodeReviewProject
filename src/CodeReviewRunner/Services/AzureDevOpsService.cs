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
                            return await ProcessPullRequestCommits(organization, project, repositoryId, pullRequestId, response, cancellationToken);
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

        // Load existing threads to avoid duplicate comments
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var existingResp = await _httpClient.GetAsync(url, cancellationToken);
            if (existingResp.IsSuccessStatusCode)
            {
                var existingJson = await existingResp.Content.ReadAsStringAsync(cancellationToken);
                using var existingDoc = System.Text.Json.JsonDocument.Parse(existingJson);
                if (existingDoc.RootElement.TryGetProperty("value", out var threadsEl) && threadsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var thread in threadsEl.EnumerateArray())
                    {
                        string? path = null;
                        int? line = null;
                        if (thread.TryGetProperty("threadContext", out var ctx) && ctx.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            if (ctx.TryGetProperty("filePath", out var fp) && fp.ValueKind == System.Text.Json.JsonValueKind.String)
                                path = fp.GetString();
                            if (ctx.TryGetProperty("rightFileStart", out var rfs) && rfs.ValueKind == System.Text.Json.JsonValueKind.Object && rfs.TryGetProperty("line", out var le))
                                line = le.GetInt32();
                        }
                        if (thread.TryGetProperty("comments", out var commentsArr) && commentsArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var c in commentsArr.EnumerateArray())
                            {
                                var content = c.TryGetProperty("content", out var ce) && ce.ValueKind == System.Text.Json.JsonValueKind.String ? ce.GetString() : null;
                                if (!string.IsNullOrEmpty(path) && line.HasValue && !string.IsNullOrEmpty(content))
                                {
                                    existing.Add($"{path}|{line.Value}|{content}");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed loading existing PR threads for dedup");
        }

        var allowedSet = allowedFilePaths != null
            ? new HashSet<string>(allowedFilePaths.Select(p => NormalizeForCompare(repositoryPath, p)), StringComparer.OrdinalIgnoreCase)
            : null;

        var commentCount = 0;
        var postedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("Processing {IssueCount} issues for comments (MaxCommentsPerFile: {MaxComments})",
            issues.Count(), _options.Notifications.MaxCommentsPerFile);

        foreach (var issue in issues.Take(_options.Notifications.MaxCommentsPerFile))
        {
            _logger.LogInformation("Processing issue: Rule {RuleId}, Severity {Severity}, File {FilePath}, Line {Line}",
                issue.RuleId, issue.Severity, issue.FilePath, issue.Line);

            // Compute repo-relative normalized path, avoid traversals
            var rel = Path.GetRelativePath(repositoryPath, issue.FilePath).Replace('\\', '/');
            if (rel.StartsWith("../"))
            {
                // Fall back to using the tail from original path in repo-style format
                rel = issue.FilePath.Replace('\\', '/').TrimStart('/');
                _logger.LogInformation("Adjusted file path to: {RelativePath}", rel);
            }
            var relativePath = rel.StartsWith('/') ? rel : "/" + rel;

            if (allowedSet != null)
            {
                var normalizedIssuePath = relativePath.TrimStart('/').Replace('\\', '/');
                _logger.LogInformation("Checking if file is in PR changes: {NormalizedPath}", normalizedIssuePath);

                // Try different path normalizations until we find a match
                var possiblePaths = new[]
                {
                    normalizedIssuePath,
                    normalizedIssuePath.TrimStart('/'),
                    "Services/" + normalizedIssuePath.TrimStart('/'),
                    normalizedIssuePath.TrimStart('/').Replace("Services/", ""),
                    normalizedIssuePath.Replace("/Services/", "/")
                };

                var pathExists = possiblePaths.Any(p => allowedSet.Contains(p));
                if (!pathExists)
                {
                    _logger.LogWarning("Skip commenting on {FilePath} (not in PR changed files). RuleId: {RuleId}", issue.FilePath, issue.RuleId);
                    _logger.LogDebug("Tried paths: {Paths}", string.Join(", ", possiblePaths));
                    _logger.LogDebug("Available paths in PR: {AllowedPaths}", string.Join(", ", allowedSet));
                    continue;
                }
            }

            var contentText = $"{issue.Severity.ToUpper()}: {issue.Message} (rule {issue.RuleId})";
            var body = new
            {
                comments = new[] {
                    new {
                        parentCommentId = 0,
                        content = contentText,
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

            // Skip duplicate if same path/line/content already exists
            var key = $"{relativePath}|{issue.Line}|{contentText}";
            if (existing.Contains(key))
            {
                _logger.LogWarning("Skip duplicate comment for {Path} line {Line}, RuleId: {RuleId}",
                    relativePath, issue.Line, issue.RuleId);
                continue;
            }

            // Skip duplicates within this run, too
            if (postedThisRun.Contains(key))
            {
                _logger.LogWarning("Skip same-run duplicate for {Path} line {Line}, RuleId: {RuleId}",
                    relativePath, issue.Line, issue.RuleId);
                continue;
            }

            var response = await _httpClient.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
            _logger.LogDebug("Post comment on {RelativePath} line {Line}: {StatusCode}", relativePath, issue.Line, response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                existing.Add(key);
                postedThisRun.Add(key);
            }
            commentCount++;
        }

        _logger.LogInformation("Posted {CommentCount} comments to Azure DevOps", commentCount);
    }

    public Task PostSummaryAsync(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        List<CodeIssue> issues,
        CancellationToken cancellationToken = default)
    {
        // Summary has been disabled as requested
        _logger.LogInformation("Summary posting is permanently disabled");
        return Task.CompletedTask;
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
        string pullRequestId,
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
                        var changes = await GetChangesBetweenCommits(organization, project, repositoryId, pullRequestId, firstCommitId.GetString()!, lastCommitId.GetString()!, cancellationToken);
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
        string pullRequestId,
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
                                // Filter by supported extensions early
                                var isAnalyzableFile = _options.Analysis.SupportedFileExtensions.Any(ext =>
                                    candidate.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
                                if (isAnalyzableFile)
                                {
                                    changedPaths.Add(candidate);
                                    addedThisPage++;
                                }
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
            if (changedPaths.Count == 0)
            {
                _logger.LogInformation("No paths from commit diff; falling back to PR iterations changes");
                var iterFiles = await GetChangedFilesFromIterations(organization, project, repositoryId, pullRequestId, cancellationToken);
                if (iterFiles.Any())
                {
                    changedPaths.AddRange(iterFiles);
                }
            }

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

    private async Task<List<string>> GetChangedFilesFromIterations(
        string organization,
        string project,
        string repositoryId,
        string pullRequestId,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        try
        {
            var iterationsUrl = $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/iterations?api-version={_options.AzureDevOps.ApiVersion}";
            var resp = await _httpClient.GetAsync(iterationsUrl, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to list iterations: {Status}", resp.StatusCode);
                return paths;
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var arr) || arr.GetArrayLength() == 0)
                return paths;

            var lastIterationId = arr.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).DefaultIfEmpty().Max();
            var changesUrl = $"{organization.TrimEnd('/')}/{project}/_apis/git/repositories/{repositoryId}/pullRequests/{pullRequestId}/iterations/{lastIterationId}/changes?api-version={_options.AzureDevOps.ApiVersion}";
            var changesResp = await _httpClient.GetAsync(changesUrl, cancellationToken);
            if (!changesResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get iteration changes: {Status}", changesResp.StatusCode);
                return paths;
            }

            var changesJson = await changesResp.Content.ReadAsStringAsync(cancellationToken);
            using var changesDoc = System.Text.Json.JsonDocument.Parse(changesJson);
            if (changesDoc.RootElement.TryGetProperty("changeEntries", out var entries))
            {
                foreach (var e in entries.EnumerateArray())
                {
                    if (e.TryGetProperty("item", out var item) && item.TryGetProperty("path", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        if (e.TryGetProperty("changeType", out var ct) && string.Equals(ct.GetString(), "delete", StringComparison.OrdinalIgnoreCase))
                            continue;
                        paths.Add(p.GetString() ?? string.Empty);
                    }
                }
            }
            return paths;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Iteration fallback failed");
            return paths;
        }
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
        if (string.IsNullOrEmpty(fullPath)) return string.Empty;

        // Normalize path separators to forward slash
        var normalized = fullPath.Replace('\\', '/').TrimStart('/');

        // Handle both with and without 'Services/' prefix
        if (normalized.StartsWith("Services/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("Services/".Length);
        }

        // If we have a repo path, try to make the path relative to it
        if (!string.IsNullOrEmpty(repoPath))
        {
            var normalizedRepoPath = repoPath.Replace('\\', '/').TrimStart('/');
            if (normalized.StartsWith(normalizedRepoPath, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(normalizedRepoPath.Length);
            }
        }

        return normalized.Trim('/');
    }
}
