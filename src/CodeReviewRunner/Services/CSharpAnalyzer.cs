using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;

namespace CodeReviewRunner.Services;

public class CSharpAnalyzer
{
    public List<CodeIssue> Analyze(string repoPath, JObject rules, IEnumerable<string>? limitToFiles = null)
    {
        var issues = new List<CodeIssue>();
        IEnumerable<string> csFiles;
        if (limitToFiles != null)
        {
            csFiles = limitToFiles.Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(p));
        }
        else
        {
            csFiles = Directory.EnumerateFiles(
                repoPath,
                "*.cs",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                }
            );
        }

        Console.WriteLine($"CSharpAnalyzer: Processing {csFiles.Count()} C# files");
        foreach (var file in csFiles)
        {
            Console.WriteLine($"  Analyzing: {file}");
            var text = File.ReadAllText(file);
            var ruleSet = (JArray?)rules["csharp"]?["rules"];
            foreach (var rule in ruleSet ?? new JArray())
            {
                var type = (string?)rule["type"];
                var pattern = (string?)rule["pattern"];
                var message = (string?)rule["message"];
                var severity = (string?)rule["severity"];
                var id = (string?)rule["id"];

                if (type == "forbidden" && !string.IsNullOrEmpty(pattern) && text.Contains(pattern))
                {
                    var line = GetLineNumber(text, pattern);
                    issues.Add(new CodeIssue
                    {
                        FilePath = file,
                        Line = line,
                        Message = message ?? "Rule violation",
                        Severity = severity ?? "error",
                        RuleId = id ?? "CS000"
                    });
                }
            }
        }
        return issues;
    }

    private int GetLineNumber(string text, string pattern)
    {
        var index = text.IndexOf(pattern);
        return index < 0 ? 1 : text.Substring(0, index).Split('\n').Length;
    }
}