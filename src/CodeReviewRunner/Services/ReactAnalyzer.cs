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
        Console.WriteLine($"ReactAnalyzer: Processing {targetFiles.Count()} JS/TS files");
        if (!targetFiles.Any())
            return results;
        var configPath = Path.Combine(Path.GetTempPath(), "eslint-config.json");
        File.WriteAllText(configPath, ExtractEslintConfig(rules));

        var filesArg = string.Join(" ", targetFiles.Select(f => $"\"{f}\""));
        var psi = new ProcessStartInfo("npx", $"--yes eslint@8 -f json -c \"{configPath}\" {filesArg}")
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

    public List<CodeIssue> AnalyzeFromContent(JObject rules, IEnumerable<(string path, string content)> files)
    {
        var results = new List<CodeIssue>();

        Console.WriteLine($"ReactAnalyzer: Processing {files.Count()} JS/TS files from content");
        if (!files.Any())
            return results;

        var configPath = Path.Combine(Path.GetTempPath(), "eslint-config.json");
        File.WriteAllText(configPath, ExtractEslintConfig(rules));

        // Create temporary files for ESLint to analyze
        var tempFiles = new List<string>();
        try
        {
            foreach (var (path, content) in files)
            {
                var tempFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(path));
                File.WriteAllText(tempFile, content);
                tempFiles.Add(tempFile);
            }

            var filesArg = string.Join(" ", tempFiles.Select(f => $"\"{f}\""));
            var psi = new ProcessStartInfo("npx", $"--yes eslint@8 -f json -c \"{configPath}\" {filesArg}")
            {
                WorkingDirectory = Path.GetTempPath(),
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
            var fileIndex = 0;
            foreach (var file in arr)
            {
                var originalPath = files.ElementAt(fileIndex).path;
                fileIndex++;

                foreach (var msg in file["messages"]!)
                {
                    results.Add(new CodeIssue
                    {
                        FilePath = originalPath,
                        Line = (int?)msg["line"] ?? 1,
                        Message = (string?)msg["message"] ?? "ESLint issue",
                        Severity = ((int?)msg["severity"] == 2 ? "error" : "warning"),
                        RuleId = (string?)msg["ruleId"] ?? "JS000"
                    });
                }
            }
        }
        finally
        {
            // Clean up temporary files
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not delete temporary file {tempFile}: {ex.Message}");
                }
            }
        }

        return results;
    }
}

static string ExtractEslintConfig(Newtonsoft.Json.Linq.JObject rules)
    {
        var direct = rules["javascript"]?["eslintOverride"]?.ToString();
        if (!string.IsNullOrWhiteSpace(direct)) return direct!;

        // Fallback: map top-level rules to a minimal ESLint config
        // Only handle a couple of common ones for now
        var cfg = new Newtonsoft.Json.Linq.JObject
        {
            ["rules"] = new Newtonsoft.Json.Linq.JObject()
        };
        if (rules["rules"] is Newtonsoft.Json.Linq.JArray all)
        {
            foreach (var r in all)
            {
                var langs = r["languages"] as Newtonsoft.Json.Linq.JArray;
                if (langs != null && langs.Any(l => string.Equals((string?)l, "javascript", StringComparison.OrdinalIgnoreCase) || string.Equals((string?)l, "typescript", StringComparison.OrdinalIgnoreCase)))
                {
                    var id = (string?)r["id"];
                    if (string.Equals(id, "no-console", StringComparison.OrdinalIgnoreCase))
                    {
                        cfg["rules"]!["no-console"] = "error";
                    }
                    if (string.Equals(id, "camelcase", StringComparison.OrdinalIgnoreCase))
                    {
                        cfg["rules"]!["camelcase"] = "warn";
                    }
                }
            }
        }
        return cfg.ToString();
    }