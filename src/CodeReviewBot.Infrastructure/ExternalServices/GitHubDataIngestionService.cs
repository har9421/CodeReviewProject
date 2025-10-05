using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class GitHubDataIngestionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubDataIngestionService> _logger;
    private readonly ILearningService _learningService;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly string _githubToken;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly Dictionary<string, DateTime> _lastRequestTime = new();

    public GitHubDataIngestionService(
        HttpClient httpClient,
        ILogger<GitHubDataIngestionService> logger,
        ILearningService learningService,
        ICodeAnalyzer codeAnalyzer)
    {
        _httpClient = httpClient;
        _logger = logger;
        _learningService = learningService;
        _codeAnalyzer = codeAnalyzer;
        _githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
        _rateLimitSemaphore = new SemaphoreSlim(1, 1); // GitHub allows 5000 requests/hour for authenticated users
    }

    public async Task<IngestionProgress> StartBulkIngestionAsync(IngestionConfig config)
    {
        var progress = new IngestionProgress
        {
            Id = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow,
            Config = config,
            Status = IngestionStatus.Running
        };

        try
        {
            _logger.LogInformation("Starting bulk ingestion with config: {Config}", JsonSerializer.Serialize(config));

            // Get repositories to process
            var repositories = await GetRepositoriesAsync(config);
            progress.TotalRepositories = repositories.Count;

            var processedPRs = 0;
            var totalIssues = 0;
            var totalComments = 0;

            foreach (var repo in repositories)
            {
                try
                {
                    _logger.LogInformation("Processing repository: {Owner}/{Name}", repo.Owner, repo.Name);

                    var repoProgress = await ProcessRepositoryAsync(repo, config, progress);
                    processedPRs += repoProgress.ProcessedPRs;
                    totalIssues += repoProgress.TotalIssues;
                    totalComments += repoProgress.TotalComments;

                    progress.ProcessedRepositories++;
                    progress.ProcessedPRs = processedPRs;
                    progress.TotalIssues = totalIssues;
                    progress.TotalComments = totalComments;

                    // Save progress periodically
                    if (progress.ProcessedRepositories % 10 == 0)
                    {
                        await SaveProgressAsync(progress);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing repository {Owner}/{Name}", repo.Owner, repo.Name);
                    progress.FailedRepositories++;
                }
            }

            progress.Status = IngestionStatus.Completed;
            progress.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Bulk ingestion completed. Processed {PRs} PRs from {Repos} repositories in {Duration}",
                processedPRs, progress.ProcessedRepositories, progress.Duration);

            return progress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk ingestion failed");
            progress.Status = IngestionStatus.Failed;
            progress.ErrorMessage = ex.Message;
            progress.EndTime = DateTime.UtcNow;
            return progress;
        }
    }

    private async Task<List<GitHubRepository>> GetRepositoriesAsync(IngestionConfig config)
    {
        var repositories = new List<GitHubRepository>();

        if (config.UsePopularRepositories)
        {
            // Get popular C# repositories
            repositories.AddRange(await GetPopularCSharpRepositoriesAsync(config.MaxRepositories));
        }

        if (config.CustomRepositories?.Any() == true)
        {
            // Add custom repositories
            foreach (var repo in config.CustomRepositories)
            {
                repositories.Add(new GitHubRepository
                {
                    Owner = repo.Split('/')[0],
                    Name = repo.Split('/')[1],
                    FullName = repo
                });
            }
        }

        return repositories.DistinctBy(r => r.FullName).Take(config.MaxRepositories).ToList();
    }

    private async Task<List<GitHubRepository>> GetPopularCSharpRepositoriesAsync(int maxRepos)
    {
        var repositories = new List<GitHubRepository>();
        var page = 1;
        const int perPage = 100;

        while (repositories.Count < maxRepos && page <= 10) // GitHub API limits to 1000 results
        {
            try
            {
                await EnforceRateLimitAsync();

                var url = $"https://api.github.com/search/repositories?q=language:csharp+stars:>1000&sort=stars&order=desc&page={page}&per_page={perPage}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch repositories page {Page}: {StatusCode}", page, response.StatusCode);
                    break;
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<GitHubSearchResult>(content);

                if (searchResult?.Items == null)
                    break;

                foreach (var item in searchResult.Items)
                {
                    repositories.Add(new GitHubRepository
                    {
                        Owner = item.Owner.Login,
                        Name = item.Name,
                        FullName = item.FullName,
                        Stars = item.Stars,
                        Language = item.Language
                    });
                }

                page++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching popular repositories page {Page}", page);
                break;
            }
        }

        return repositories.Take(maxRepos).ToList();
    }

    private async Task<RepositoryProgress> ProcessRepositoryAsync(GitHubRepository repo, IngestionConfig config, IngestionProgress overallProgress)
    {
        var repoProgress = new RepositoryProgress
        {
            Repository = repo,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Get pull requests for the repository
            var pullRequests = await GetPullRequestsAsync(repo, config);
            repoProgress.TotalPRs = pullRequests.Count;

            foreach (var pr in pullRequests)
            {
                try
                {
                    var prData = await ProcessPullRequestAsync(repo, pr, config);
                    if (prData != null)
                    {
                        repoProgress.ProcessedPRs++;
                        repoProgress.TotalIssues += prData.Metrics.TotalIssuesFound;
                        repoProgress.TotalComments += prData.Metrics.CommentsPosted;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing PR {Number} in {Owner}/{Name}", pr.Number, repo.Owner, repo.Name);
                    repoProgress.FailedPRs++;
                }

                // Rate limiting
                await Task.Delay(100);
            }

            repoProgress.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Completed repository {Owner}/{Name}: {ProcessedPRs}/{TotalPRs} PRs processed",
                repo.Owner, repo.Name, repoProgress.ProcessedPRs, repoProgress.TotalPRs);

            return repoProgress;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing repository {Owner}/{Name}", repo.Owner, repo.Name);
            repoProgress.ErrorMessage = ex.Message;
            return repoProgress;
        }
    }

    private async Task<List<GitHubPullRequest>> GetPullRequestsAsync(GitHubRepository repo, IngestionConfig config)
    {
        var pullRequests = new List<GitHubPullRequest>();
        var page = 1;
        const int perPage = 100;

        while (pullRequests.Count < config.MaxPRsPerRepository && page <= 10)
        {
            try
            {
                await EnforceRateLimitAsync();

                var url = $"https://api.github.com/repos/{repo.FullName}/pulls?state=closed&sort=updated&direction=desc&page={page}&per_page={perPage}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch PRs for {Owner}/{Name} page {Page}: {StatusCode}",
                        repo.Owner, repo.Name, page, response.StatusCode);
                    break;
                }

                var content = await response.Content.ReadAsStringAsync();
                var prs = JsonSerializer.Deserialize<List<GitHubPullRequest>>(content);

                if (prs == null || !prs.Any())
                    break;

                // Filter for C# PRs and date range
                var filteredPRs = prs.Where(pr =>
                    pr.CreatedAt >= config.StartDate &&
                    pr.CreatedAt <= config.EndDate &&
                    HasCSharpChanges(pr)
                ).ToList();

                pullRequests.AddRange(filteredPRs);
                page++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PRs for {Owner}/{Name} page {Page}", repo.Owner, repo.Name, page);
                break;
            }
        }

        return pullRequests.Take(config.MaxPRsPerRepository).ToList();
    }

    private bool HasCSharpChanges(GitHubPullRequest pr)
    {
        // Check if PR has C# file changes
        return pr.Files?.Any(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private async Task<LearningData?> ProcessPullRequestAsync(GitHubRepository repo, GitHubPullRequest pr, IngestionConfig config)
    {
        try
        {
            // Get PR details
            var prDetails = await GetPullRequestDetailsAsync(repo, pr.Number);
            if (prDetails == null)
                return null;

            // Get file changes
            var fileChanges = await GetPullRequestFilesAsync(repo, pr.Number);
            if (!fileChanges.Any())
                return null;

            // Convert to our format
            var convertedFileChanges = fileChanges.Select(fc => new FileChange
            {
                Path = fc.Filename,
                ChangeType = fc.Status,
                Content = fc.Patch ?? "",
                ChangedLines = ExtractChangedLines(fc.Patch),
                AnalyzeOnlyChangedLines = true
            }).ToList();

            // Analyze files
            var allIssues = new List<CodeIssue>();
            var ruleUsageCount = new Dictionary<string, int>();

            foreach (var fileChange in convertedFileChanges)
            {
                if (fileChange.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var issues = await _codeAnalyzer.AnalyzeFileAsync(fileChange);
                    allIssues.AddRange(issues);

                    foreach (var issue in issues)
                    {
                        ruleUsageCount[issue.RuleId] = ruleUsageCount.GetValueOrDefault(issue.RuleId, 0) + 1;
                    }
                }
            }

            // Create learning data
            var learningData = new LearningData
            {
                Id = Guid.NewGuid().ToString(),
                PullRequestId = pr.Number.ToString(),
                RepositoryName = repo.FullName,
                ProjectName = repo.Owner,
                AnalysisDate = pr.CreatedAt,
                Metrics = new PullRequestMetrics
                {
                    TotalFilesAnalyzed = convertedFileChanges.Count,
                    TotalIssuesFound = allIssues.Count,
                    CommentsPosted = 0, // Historical data doesn't have comments
                    FileTypes = convertedFileChanges.Select(fc => Path.GetExtension(fc.Path)).Distinct().ToList(),
                    RuleUsageCount = ruleUsageCount
                }
            };

            // Record learning data
            await _learningService.RecordPullRequestAnalysisAsync(learningData);

            return learningData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PR {Number} in {Owner}/{Name}", pr.Number, repo.Owner, repo.Name);
            return null;
        }
    }

    private async Task<GitHubPullRequestDetails?> GetPullRequestDetailsAsync(GitHubRepository repo, int prNumber)
    {
        try
        {
            await EnforceRateLimitAsync();

            var url = $"https://api.github.com/repos/{repo.FullName}/pulls/{prNumber}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubPullRequestDetails>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching PR details for {Owner}/{Name}#{Number}", repo.Owner, repo.Name, prNumber);
            return null;
        }
    }

    private async Task<List<GitHubPullRequestFile>> GetPullRequestFilesAsync(GitHubRepository repo, int prNumber)
    {
        try
        {
            await EnforceRateLimitAsync();

            var url = $"https://api.github.com/repos/{repo.FullName}/pulls/{prNumber}/files";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<GitHubPullRequestFile>();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<GitHubPullRequestFile>>(content) ?? new List<GitHubPullRequestFile>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching PR files for {Owner}/{Name}#{Number}", repo.Owner, repo.Name, prNumber);
            return new List<GitHubPullRequestFile>();
        }
    }

    private List<int> ExtractChangedLines(string? patch)
    {
        if (string.IsNullOrEmpty(patch))
            return new List<int>();

        var changedLines = new List<int>();
        var lines = patch.Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("@@"))
            {
                var match = Regex.Match(line, @"@@\s*-\d+,\d+\s*\+(\d+),(\d+)");
                if (match.Success)
                {
                    var startLine = int.Parse(match.Groups[1].Value);
                    var lineCount = int.Parse(match.Groups[2].Value);

                    for (int i = 0; i < lineCount; i++)
                    {
                        changedLines.Add(startLine + i);
                    }
                }
            }
        }

        return changedLines;
    }

    private async Task EnforceRateLimitAsync()
    {
        await _rateLimitSemaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var lastRequest = _lastRequestTime.GetValueOrDefault("github", DateTime.MinValue);

            if (now - lastRequest < TimeSpan.FromMilliseconds(100)) // 10 requests per second max
            {
                await Task.Delay(100);
            }

            _lastRequestTime["github"] = now;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private async Task SaveProgressAsync(IngestionProgress progress)
    {
        try
        {
            var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync($"ingestion-progress-{progress.Id}.json", json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save progress");
        }
    }

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
    }
}

// Data models
public class IngestionConfig
{
    public bool UsePopularRepositories { get; set; } = true;
    public List<string>? CustomRepositories { get; set; }
    public int MaxRepositories { get; set; } = 100;
    public int MaxPRsPerRepository { get; set; } = 1000;
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddYears(-2);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public List<string> Languages { get; set; } = new() { "csharp" };
}

public class IngestionProgress
{
    public string Id { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime - StartTime;
    public IngestionStatus Status { get; set; }
    public IngestionConfig Config { get; set; } = new();
    public int TotalRepositories { get; set; }
    public int ProcessedRepositories { get; set; }
    public int FailedRepositories { get; set; }
    public int ProcessedPRs { get; set; }
    public int TotalIssues { get; set; }
    public int TotalComments { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RepositoryProgress
{
    public GitHubRepository Repository { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => EndTime - StartTime;
    public int TotalPRs { get; set; }
    public int ProcessedPRs { get; set; }
    public int FailedPRs { get; set; }
    public int TotalIssues { get; set; }
    public int TotalComments { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum IngestionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Paused
}

public class GitHubRepository
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Language { get; set; } = string.Empty;
}

public class GitHubPullRequest
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public List<string>? Files { get; set; }
}

public class GitHubPullRequestDetails
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string State { get; set; } = string.Empty;
    public GitHubUser User { get; set; } = new();
}

public class GitHubPullRequestFile
{
    public string Filename { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Patch { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
}

public class GitHubUser
{
    public string Login { get; set; } = string.Empty;
    public int Id { get; set; }
}

public class GitHubSearchResult
{
    public List<GitHubSearchItem> Items { get; set; } = new();
}

public class GitHubSearchItem
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int Stars { get; set; }
    public GitHubUser Owner { get; set; } = new();
}
