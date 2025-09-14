using CodeReviewRunner.Interfaces;
using CodeReviewRunner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeReviewRunner.Configuration;

namespace CodeReviewRunner;

public class CodeReviewApplication
{
    private readonly ICodeReviewService _codeReviewService;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<CodeReviewApplication> _logger;
    private readonly CodeReviewOptions _options;

    public CodeReviewApplication(
        ICodeReviewService codeReviewService,
        IAzureDevOpsService azureDevOpsService,
        ILogger<CodeReviewApplication> logger,
        IOptions<CodeReviewOptions> options)
    {
        _codeReviewService = codeReviewService;
        _azureDevOpsService = azureDevOpsService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length < 6)
            {
                _logger.LogError("Invalid arguments. Expected: <repoPath> <rulesUrl> <prId> <orgUrl> <project> <repoId>");
                _logger.LogInformation("For local testing: dotnet run test <rulesUrl> test <orgUrl> <project> <repoId>");
                return 1;
            }

            var repoPath = args[0];
            var rulesUrl = args[1];
            var prId = args[2];
            var orgUrl = args[3];
            var project = args[4];
            var repoId = args[5];

            // Check if we're in test mode
            bool isTestMode = repoPath.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                             prId.Equals("test", StringComparison.OrdinalIgnoreCase);

            if (isTestMode)
            {
                return await RunTestModeAsync(rulesUrl, orgUrl, project, repoId);
            }
            else
            {
                return await RunProductionModeAsync(repoPath, rulesUrl, prId, orgUrl, project, repoId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application error");
            return 1;
        }
    }

    private async Task<int> RunTestModeAsync(string rulesUrl, string orgUrl, string project, string repoId)
    {
        _logger.LogInformation("=== RUNNING IN TEST MODE ===");
        _logger.LogInformation("Analyzing local test files instead of Azure DevOps PR");

        var testFiles = GetLocalTestFiles();
        var result = await _codeReviewService.AnalyzeLocalFilesAsync(testFiles.Select(f => f.path));

        LogResults(result);
        return result.ErrorCount > 0 ? 1 : 0;
    }

    private async Task<int> RunProductionModeAsync(
        string repoPath, string rulesUrl, string prId, string orgUrl, string project, string repoId)
    {
        var pat = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        if (string.IsNullOrWhiteSpace(pat))
        {
            _logger.LogError("SYSTEM_ACCESSTOKEN is not set. Enable 'Allow scripts to access OAuth token' in the pipeline or set a PAT.");
            _logger.LogInformation("For local testing, use: dotnet run test <rulesUrl> test <orgUrl> <project> <repoId>");
            return 1;
        }

        _logger.LogInformation("Starting pull request analysis for PR {PullRequestId} in repository {RepositoryId}",
            prId, repoId);

        var result = await _codeReviewService.AnalyzePullRequestAsync(orgUrl, project, repoId, prId);

        if (!result.Success)
        {
            _logger.LogError("Analysis failed: {Errors}", string.Join(", ", result.Errors));
            return 1;
        }

        LogResults(result);

        // Post comments to Azure DevOps if enabled
        if (_options.Notifications.EnableComments && result.Issues.Any())
        {
            try
            {
                await _azureDevOpsService.PostCommentsAsync(
                    orgUrl, project, repoId, prId, repoPath, result.Issues);
                _logger.LogInformation("Posted {CommentCount} comments to Azure DevOps", result.Issues.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post comments to Azure DevOps");
            }
        }

        // Post summary if enabled
        if (_options.Notifications.EnableSummary && result.Issues.Any())
        {
            try
            {
                await _azureDevOpsService.PostSummaryAsync(
                    orgUrl, project, repoId, prId, result.Issues);
                _logger.LogInformation("Posted summary to Azure DevOps");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post summary to Azure DevOps");
            }
        }

        return result.ErrorCount > 0 ? 1 : 0;
    }

    private void LogResults(CodeReviewResult result)
    {
        _logger.LogInformation("Analysis completed in {Duration}ms", result.Duration.TotalMilliseconds);
        _logger.LogInformation("Files analyzed: {FileCount}", result.FilesAnalyzed);
        _logger.LogInformation("Total issues: {IssueCount} (Errors: {ErrorCount}, Warnings: {WarningCount})",
            result.TotalIssues, result.ErrorCount, result.WarningCount);

        if (result.Errors.Any())
        {
            _logger.LogError("Errors: {Errors}", string.Join(", ", result.Errors));
        }

        if (result.Warnings.Any())
        {
            _logger.LogWarning("Warnings: {Warnings}", string.Join(", ", result.Warnings));
        }

        if (result.Issues.Any())
        {
            _logger.LogInformation("=== ISSUES FOUND ===");
            foreach (var issue in result.Issues.Take(10)) // Log first 10 issues
            {
                _logger.LogInformation("{Severity}: {Message} in {FilePath} at line {Line} (rule: {RuleId})",
                    issue.Severity.ToUpper(), issue.Message, issue.FilePath, issue.Line, issue.RuleId);
            }

            if (result.Issues.Count > 10)
            {
                _logger.LogInformation("... and {MoreCount} more issues", result.Issues.Count - 10);
            }
        }
        else
        {
            _logger.LogInformation("No issues found");
        }
    }

    private static List<(string path, string content)> GetLocalTestFiles()
    {
        var testFiles = new List<(string path, string content)>();

        // Sample C# file with issues
        var csContent = @"using System;

public class TestClass
{
    public void MethodWithLongNameThatExceedsTheMaximumAllowedLength()
    {
        // This method name is too long
        Console.WriteLine(""Hello World"");
    }
    
    public void GoodMethod()
    {
        // This method name is fine
        Console.WriteLine(""Hello World"");
    }
    
    public void AnotherMethodWithVeryLongNameThatShouldTriggerWarning()
    {
        // Another long method name
        var unusedVariable = ""This variable is not used"";
    }
}";

        // Sample React/TypeScript file with issues
        var tsxContent = @"import React from 'react';

const ComponentWithLongNameThatExceedsTheMaximumAllowedLength = () => {
    // This component name is too long
    return <div>Hello World</div>;
};

const GoodComponent = () => {
    // This component name is fine
    return <div>Hello World</div>;
};

const AnotherComponentWithVeryLongNameThatShouldTriggerWarning = () => {
    // Another long component name
    const unusedVariable = 'This variable is not used';
    return <div>Test</div>;
};

export default GoodComponent;";

        // Sample JavaScript file
        var jsContent = @"function functionWithLongNameThatExceedsTheMaximumAllowedLength() {
    // This function name is too long
    console.log('Hello World');
}

function goodFunction() {
    // This function name is fine
    console.log('Hello World');
}

const unusedVariable = 'This variable is not used';
";

        testFiles.Add(("test-files/sample.cs", csContent));
        testFiles.Add(("test-files/sample.tsx", tsxContent));
        testFiles.Add(("test-files/sample.js", jsContent));

        return testFiles;
    }
}
