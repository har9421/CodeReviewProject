using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace CodeReviewBot.Infrastructure.ExternalServices;

public class BatchProcessingService
{
    private readonly ILogger<BatchProcessingService> _logger;
    private readonly ILearningService _learningService;
    private readonly ICodeAnalyzer _codeAnalyzer;
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly ConcurrentQueue<ProcessingBatch> _batchQueue = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _processingTask;

    public BatchProcessingService(
        ILogger<BatchProcessingService> logger,
        ILearningService learningService,
        ICodeAnalyzer codeAnalyzer)
    {
        _logger = logger;
        _learningService = learningService;
        _codeAnalyzer = codeAnalyzer;
        _processingSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

        // Start background processing task
        _processingTask = Task.Run(ProcessBatchesAsync);
    }

    public async Task<string> QueueBatchAsync(BatchProcessingRequest request)
    {
        var batchId = Guid.NewGuid().ToString();
        var batch = new ProcessingBatch
        {
            Id = batchId,
            Request = request,
            Status = BatchStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        _batchQueue.Enqueue(batch);

        _logger.LogInformation("Queued batch {BatchId} with {ItemCount} items", batchId, request.Items.Count);

        return batchId;
    }

    public async Task<BatchStatus> GetBatchStatusAsync(string batchId)
    {
        // In a real implementation, you'd store this in a database
        // For now, we'll return a simple status
        return BatchStatus.Processing;
    }

    private async Task ProcessBatchesAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (_batchQueue.TryDequeue(out var batch))
                {
                    await ProcessBatchAsync(batch);
                }
                else
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch processing loop");
                await Task.Delay(5000, _cancellationTokenSource.Token);
            }
        }
    }

    private async Task ProcessBatchAsync(ProcessingBatch batch)
    {
        batch.Status = BatchStatus.Processing;
        batch.StartedAt = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing batch {BatchId} with {ItemCount} items",
                batch.Id, batch.Request.Items.Count);

            var results = new List<ProcessingResult>();
            var semaphore = new SemaphoreSlim(batch.Request.MaxConcurrency, batch.Request.MaxConcurrency);

            var tasks = batch.Request.Items.Select(async item =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await ProcessItemAsync(item);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var itemResults = await Task.WhenAll(tasks);
            results.AddRange(itemResults);

            // Process results in batches for learning service
            await ProcessResultsInBatchesAsync(results, batch.Request.BatchSize);

            batch.Status = BatchStatus.Completed;
            batch.CompletedAt = DateTime.UtcNow;
            batch.ProcessedItems = results.Count;
            batch.SuccessfulItems = results.Count(r => r.Success);
            batch.FailedItems = results.Count(r => !r.Success);

            _logger.LogInformation("Completed batch {BatchId}: {ProcessedItems} processed, {SuccessfulItems} successful, {FailedItems} failed",
                batch.Id, batch.ProcessedItems, batch.SuccessfulItems, batch.FailedItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch {BatchId}", batch.Id);
            batch.Status = BatchStatus.Failed;
            batch.ErrorMessage = ex.Message;
            batch.CompletedAt = DateTime.UtcNow;
        }
    }

    private async Task<ProcessingResult> ProcessItemAsync(ProcessingItem item)
    {
        try
        {
            switch (item.Type)
            {
                case ProcessingItemType.PullRequest:
                    return await ProcessPullRequestItemAsync(item);
                case ProcessingItemType.Repository:
                    return await ProcessRepositoryItemAsync(item);
                case ProcessingItemType.FileChange:
                    return await ProcessFileChangeItemAsync(item);
                default:
                    return new ProcessingResult
                    {
                        ItemId = item.Id,
                        Success = false,
                        ErrorMessage = $"Unknown item type: {item.Type}"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item {ItemId}", item.Id);
            return new ProcessingResult
            {
                ItemId = item.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ProcessingResult> ProcessPullRequestItemAsync(ProcessingItem item)
    {
        try
        {
            var prData = JsonSerializer.Deserialize<PullRequestData>(item.Data);
            if (prData == null)
            {
                return new ProcessingResult
                {
                    ItemId = item.Id,
                    Success = false,
                    ErrorMessage = "Invalid pull request data"
                };
            }

            // Convert to learning data format
            var learningData = new LearningData
            {
                Id = Guid.NewGuid().ToString(),
                PullRequestId = prData.Number.ToString(),
                RepositoryName = prData.Repository,
                ProjectName = prData.Owner,
                AnalysisDate = prData.CreatedAt,
                Metrics = new PullRequestMetrics
                {
                    TotalFilesAnalyzed = prData.Files?.Count ?? 0,
                    TotalIssuesFound = 0, // Will be calculated during analysis
                    FileTypes = prData.Files?.Select(f => Path.GetExtension(f)).Distinct().ToList() ?? new List<string>()
                }
            };

            // Analyze files if provided
            if (prData.Files?.Any() == true)
            {
                var allIssues = new List<CodeIssue>();
                var ruleUsageCount = new Dictionary<string, int>();

                foreach (var file in prData.Files)
                {
                    if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileChange = new FileChange
                        {
                            Path = file,
                            ChangeType = "modified",
                            Content = prData.FileContents?.GetValueOrDefault(file, "") ?? "",
                            ChangedLines = new List<int>(), // Would be extracted from diff
                            AnalyzeOnlyChangedLines = true
                        };

                        var issues = await _codeAnalyzer.AnalyzeFileAsync(fileChange);
                        allIssues.AddRange(issues);

                        foreach (var issue in issues)
                        {
                            ruleUsageCount[issue.RuleId] = ruleUsageCount.GetValueOrDefault(issue.RuleId, 0) + 1;
                        }
                    }
                }

                learningData.Metrics.TotalIssuesFound = allIssues.Count;
                learningData.Metrics.RuleUsageCount = ruleUsageCount;
            }

            // Record learning data
            await _learningService.RecordPullRequestAnalysisAsync(learningData);

            return new ProcessingResult
            {
                ItemId = item.Id,
                Success = true,
                ProcessedData = JsonSerializer.Serialize(learningData)
            };
        }
        catch (Exception ex)
        {
            return new ProcessingResult
            {
                ItemId = item.Id,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<ProcessingResult> ProcessRepositoryItemAsync(ProcessingItem item)
    {
        // Process repository-level data
        await Task.Delay(100); // Simulate processing
        return new ProcessingResult
        {
            ItemId = item.Id,
            Success = true
        };
    }

    private async Task<ProcessingResult> ProcessFileChangeItemAsync(ProcessingItem item)
    {
        // Process individual file changes
        await Task.Delay(50); // Simulate processing
        return new ProcessingResult
        {
            ItemId = item.Id,
            Success = true
        };
    }

    private async Task ProcessResultsInBatchesAsync(List<ProcessingResult> results, int batchSize)
    {
        for (int i = 0; i < results.Count; i += batchSize)
        {
            var batch = results.Skip(i).Take(batchSize).ToList();

            // Process batch results for learning
            await ProcessLearningDataBatchAsync(batch);

            // Small delay to prevent overwhelming the system
            await Task.Delay(100);
        }
    }

    private async Task ProcessLearningDataBatchAsync(List<ProcessingResult> results)
    {
        try
        {
            // Extract learning data from successful results
            var learningDataList = new List<LearningData>();

            foreach (var result in results.Where(r => r.Success && !string.IsNullOrEmpty(r.ProcessedData)))
            {
                try
                {
                    var learningData = JsonSerializer.Deserialize<LearningData>(result.ProcessedData!);
                    if (learningData != null)
                    {
                        learningDataList.Add(learningData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize learning data for item {ItemId}", result.ItemId);
                }
            }

            // Batch process learning data
            if (learningDataList.Any())
            {
                await _learningService.RecordPullRequestAnalysisAsync(learningDataList.First());
                // In a real implementation, you'd batch process all learning data
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing learning data batch");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _processingTask.Wait(5000);
        _cancellationTokenSource.Dispose();
        _processingSemaphore.Dispose();
    }
}

// Data models
public class BatchProcessingRequest
{
    public List<ProcessingItem> Items { get; set; } = new();
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public int BatchSize { get; set; } = 100;
    public string Priority { get; set; } = "Normal";
}

public class ProcessingItem
{
    public string Id { get; set; } = string.Empty;
    public ProcessingItemType Type { get; set; }
    public string Data { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ProcessingBatch
{
    public string Id { get; set; } = string.Empty;
    public BatchProcessingRequest Request { get; set; } = new();
    public BatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProcessedItems { get; set; }
    public int SuccessfulItems { get; set; }
    public int FailedItems { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ProcessingResult
{
    public string ItemId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProcessedData { get; set; }
    public Dictionary<string, object> Metrics { get; set; } = new();
}

public enum ProcessingItemType
{
    PullRequest,
    Repository,
    FileChange
}

public enum BatchStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class PullRequestData
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string>? Files { get; set; }
    public Dictionary<string, string>? FileContents { get; set; }
}
