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
        issues.AddRange(new CSharpAnalyzer().Analyze(repoPath, rules));
        issues.AddRange(new ReactAnalyzer().Analyze(repoPath, rules));

        var ado = new AzureDevOpsClient(pat!);
        await ado.PostCommentsAsync(orgUrl, project, repoId, prId, repoPath, issues);
        //  await ado.PostSummaryAsync(orgUrl, project, repoId, prId, issues);

        return issues.Any(i => i.Severity == "error") ? 1 : 0;
    }
}