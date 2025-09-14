using CodeReviewRunner.Services;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 6)
        {
            Console.WriteLine("Usage: dotnet run <repoPath> <rulesUrl> <prId> <orgUrl> <project> <repoId>");
            return 1;
        }

        var repoPath = args[0];
        var rulesUrl = args[1];
        var prId = args[2];
        var orgUrl = args[3];
        var project = args[4];
        var repoId = args[5];
        var pat = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        if (string.IsNullOrWhiteSpace(pat))
        {
            Console.Error.WriteLine("SYSTEM_ACCESSTOKEN is not set. Enable 'Allow scripts to access OAuth token' in the pipeline or set a PAT.");
            return 1;
        }

        var fetcher = new RuleFetcher();
        var rules = await fetcher.FetchAsync(rulesUrl);

        var issues = new List<CodeReviewRunner.Models.CodeIssue>();

        var ado = new AzureDevOpsClient(pat!);
        List<(string path, string content)> changedFiles;
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

        // Never analyze the whole repo if we failed to fetch PR changes. If none found, exit cleanly.
        if (!changedFiles.Any())
        {
            Console.WriteLine("No changed files detected in PR. Skipping analysis.");
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

        await ado.PostCommentsAsync(orgUrl, project, repoId, prId, repoPath, issues, changedFiles.Select(f => f.path));
        //  await ado.PostSummaryAsync(orgUrl, project, repoId, prId, issues);

        return issues.Any(i => i.Severity == "error") ? 1 : 0;
    }
}