using System.Diagnostics;
using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;

namespace CodeReviewRunner.Services;

public class ReactAnalyzer
{
    public List<CodeIssue> Analyze(string repoPath, JObject rules, IEnumerable<string>? limitToFiles = null)
    {
        var results = new List<CodeIssue>();

        IEnumerable<string> targetFiles;
        if (limitToFiles != null)
        {
            targetFiles = limitToFiles.Where(p =>
                (p.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                 || p.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase)
                 || p.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                 || p.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase)) && File.Exists(p));
        }
        else
        {
            targetFiles = Directory.EnumerateFiles(
                repoPath,
                "*",
                new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }
            ).Where(p => p.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".jsx", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
                     || p.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));
        }
        if (!targetFiles.Any())
            return results;
        var configPath = Path.Combine(Path.GetTempPath(), "eslint-config.json");
        File.WriteAllText(configPath, rules["javascript"]?["eslintOverride"]?.ToString() ?? "{}");

        var filesArg = string.Join(" ", targetFiles.Select(f => $"\"{f}\""));
        var psi = new ProcessStartInfo("npx", $"--yes eslint@9 -f json -c \"{configPath}\" {filesArg}")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine($"ESLint failed: {error}");
            return results;
        }

        var arr = string.IsNullOrWhiteSpace(output) ? new JArray() : JArray.Parse(output);
        foreach (var file in arr)
        {
            var filePath = (string?)file["filePath"];
            foreach (var msg in file["messages"]!)
            {
                results.Add(new CodeIssue
                {
                    FilePath = filePath ?? "",
                    Line = (int?)msg["line"] ?? 1,
                    Message = (string?)msg["message"] ?? "ESLint issue",
                    Severity = ((int?)msg["severity"] == 2 ? "error" : "warning"),
                    RuleId = (string?)msg["ruleId"] ?? "JS000"
                });
            }
        }
        return results;
    }
}