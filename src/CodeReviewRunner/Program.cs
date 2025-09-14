using CodeReviewRunner.Services;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 6)
        {
            Console.WriteLine("Usage: dotnet run <repoPath> <rulesUrl> <prId> <orgUrl> <project> <repoId>");
            Console.WriteLine("For local testing: dotnet run test <rulesUrl> test <orgUrl> <project> <repoId>");
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

        var pat = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        if (!isTestMode && string.IsNullOrWhiteSpace(pat))
        {
            Console.Error.WriteLine("SYSTEM_ACCESSTOKEN is not set. Enable 'Allow scripts to access OAuth token' in the pipeline or set a PAT.");
            Console.Error.WriteLine("For local testing, use: dotnet run test <rulesUrl> test <orgUrl> <project> <repoId>");
            return 1;
        }

        var fetcher = new RuleFetcher();
        var rules = await fetcher.FetchAsync(rulesUrl);

        var issues = new List<CodeReviewRunner.Models.CodeIssue>();

        List<(string path, string content)> changedFiles;

        if (isTestMode)
        {
            Console.WriteLine("=== RUNNING IN TEST MODE ===");
            Console.WriteLine("Analyzing local files instead of Azure DevOps PR");
            changedFiles = GetLocalTestFiles();
        }
        else
        {
            var ado = new AzureDevOpsClient(pat!);
            try
            {
                Console.WriteLine($"Fetching changed files for PR {prId} in repo {repoId}");
                Console.WriteLine($"Organization URL: {orgUrl}");
                Console.WriteLine($"Project: {project}");

                // First test repository access
                Console.WriteLine("Testing repository access...");
                var repoAccess = await ado.TestRepositoryAccessAsync(orgUrl, project, repoId);
                if (!repoAccess)
                {
                    Console.Error.WriteLine("Failed to access repository. Check your credentials and repository ID.");
                    return 1;
                }

                changedFiles = await ado.GetPullRequestChangedFilesAsync(orgUrl, project, repoId, prId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to fetch changed files. {ex.Message}");
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                changedFiles = new List<(string path, string content)>();
            }
        }

        // Never analyze the whole repo if we failed to fetch PR changes. If none found, exit cleanly.
        if (!changedFiles.Any())
        {
            Console.WriteLine("No changed files detected. Skipping analysis.");
            return 0;
        }
        Console.WriteLine($"Changed files detected: {changedFiles.Count}");
        foreach (var (path, content) in changedFiles.Take(50))
        {
            Console.WriteLine($" - {path} ({content.Length} characters)");
        }

        var csIssues = new CSharpAnalyzer().AnalyzeFromContent(rules, changedFiles.Where(f => f.path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine($"C# issues: {csIssues.Count}");
        issues.AddRange(csIssues);
        var jsFiles = changedFiles.Where(f =>
            f.path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
            f.path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));
        var jsIssues = new ReactAnalyzer().AnalyzeFromContent(rules, jsFiles);
        Console.WriteLine($"JS/TS issues: {jsIssues.Count}");
        issues.AddRange(jsIssues);

        // Display issues found
        Console.WriteLine("\n=== ISSUES FOUND ===");
        foreach (var issue in issues)
        {
            Console.WriteLine($"{issue.Severity.ToUpper()}: {issue.Message} in {issue.FilePath} at line {issue.Line} (rule: {issue.RuleId})");
        }

        if (!isTestMode)
        {
            var ado = new AzureDevOpsClient(pat!);
            await ado.PostCommentsAsync(orgUrl, project, repoId, prId, repoPath, issues, changedFiles.Select(f => f.path));
            //  await ado.PostSummaryAsync(orgUrl, project, repoId, prId, issues);
        }
        else
        {
            Console.WriteLine("\n=== TEST MODE - No comments posted to Azure DevOps ===");
        }

        return issues.Any(i => i.Severity == "error") ? 1 : 0;
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

        Console.WriteLine("Using sample test files for analysis:");
        foreach (var (path, _) in testFiles)
        {
            Console.WriteLine($"  - {path}");
        }

        return testFiles;
    }
}