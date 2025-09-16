using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;
using System.Text.RegularExpressions;

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

                // Forbidden pattern check (simple substring)
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

                // Property naming check: enforce PascalCase for properties
                var appliesTo = (string?)rule["applies_to"];
                if (appliesTo == "property_declaration")
                {
                    foreach (var (lineText, lineNumber, propName) in FindPropertyDeclarations(text))
                    {
                        if (!string.IsNullOrEmpty(propName) && char.IsLower(propName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = file,
                                Line = lineNumber,
                                Message = message ?? "Property names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS004"
                            });
                        }
                    }
                }

                // Type naming check: class/interface/struct should be PascalCase
                if (appliesTo == "type_declaration")
                {
                    foreach (var (lineText, lineNumber, typeName) in FindTypeDeclarations(text))
                    {
                        if (!string.IsNullOrEmpty(typeName) && char.IsLower(typeName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = file,
                                Line = lineNumber,
                                Message = message ?? "Type names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS001"
                            });
                        }
                    }
                }

                // Method naming check: methods should be PascalCase
                if (appliesTo == "method_declaration")
                {
                    foreach (var (lineText, lineNumber, methodName) in FindMethodDeclarations(text))
                    {
                        if (!string.IsNullOrEmpty(methodName) && char.IsLower(methodName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = file,
                                Line = lineNumber,
                                Message = message ?? "Method names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS002"
                            });
                        }
                    }
                }
            }
        }
        return issues;
    }

    public List<CodeIssue> AnalyzeFromContent(JObject rules, IEnumerable<(string path, string content)> files)
    {
        var issues = new List<CodeIssue>();

        Console.WriteLine($"CSharpAnalyzer: Processing {files.Count()} C# files from content");
        foreach (var (path, content) in files)
        {
            Console.WriteLine($"  Analyzing: {path}");
            var ruleSet = (JArray?)rules["csharp"]?["rules"];
            foreach (var rule in ruleSet ?? new JArray())
            {
                var type = (string?)rule["type"];
                var pattern = (string?)rule["pattern"];
                var message = (string?)rule["message"];
                var severity = (string?)rule["severity"];
                var id = (string?)rule["id"];

                // Forbidden pattern check (simple substring)
                if (type == "forbidden" && !string.IsNullOrEmpty(pattern) && content.Contains(pattern))
                {
                    var line = GetLineNumber(content, pattern);
                    issues.Add(new CodeIssue
                    {
                        FilePath = path,
                        Line = line,
                        Message = message ?? "Rule violation",
                        Severity = severity ?? "error",
                        RuleId = id ?? "CS000"
                    });
                }

                // Property naming check: enforce PascalCase for properties
                var appliesTo = (string?)rule["applies_to"];
                if (appliesTo == "property_declaration")
                {
                    foreach (var (lineText, lineNumber, propName) in FindPropertyDeclarations(content))
                    {
                        if (!string.IsNullOrEmpty(propName) && char.IsLower(propName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = path,
                                Line = lineNumber,
                                Message = message ?? "Property names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS004"
                            });
                        }
                    }
                }

                // Type naming check: class/interface/struct should be PascalCase
                if (appliesTo == "type_declaration")
                {
                    foreach (var (lineText, lineNumber, typeName) in FindTypeDeclarations(content))
                    {
                        if (!string.IsNullOrEmpty(typeName) && char.IsLower(typeName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = path,
                                Line = lineNumber,
                                Message = message ?? "Type names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS001"
                            });
                        }
                    }
                }

                // Method naming check: methods should be PascalCase
                if (appliesTo == "method_declaration")
                {
                    foreach (var (lineText, lineNumber, methodName) in FindMethodDeclarations(content))
                    {
                        if (!string.IsNullOrEmpty(methodName) && char.IsLower(methodName![0]))
                        {
                            issues.Add(new CodeIssue
                            {
                                FilePath = path,
                                Line = lineNumber,
                                Message = message ?? "Method names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS002"
                            });
                        }
                    }
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

    private IEnumerable<(string lineText, int lineNumber, string propertyName)> FindPropertyDeclarations(string content)
    {
        // Matches C# auto-properties like: public string Role { get; set; }
        // Captures the property name in group 1
        var regex = new Regex(@"\b(public|protected|internal|private)\s+[\w<>\?\[\]]+\s+(\w+)\s*\{\s*get;", RegexOptions.Compiled);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = regex.Match(line);
            if (match.Success)
            {
                var name = match.Groups[2].Value;
                yield return (line, i + 1, name);
            }
        }
    }

    private IEnumerable<(string lineText, int lineNumber, string typeName)> FindTypeDeclarations(string content)
    {
        // Matches: public class MyClass, public interface IThing, public struct Point
        var regex = new Regex(@"\b(public|protected|internal|private)\s+(class|interface|struct)\s+(\w+)", RegexOptions.Compiled);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = regex.Match(line);
            if (match.Success)
            {
                var name = match.Groups[3].Value;
                yield return (line, i + 1, name);
            }
        }
    }

    private IEnumerable<(string lineText, int lineNumber, string methodName)> FindMethodDeclarations(string content)
    {
        // Matches methods like: public void DoWork(..., with optional generics/return types)
        var regex = new Regex(@"\b(public|protected|internal|private)\s+[\w<>\?\[\]]+\s+(\w+)\s*\(", RegexOptions.Compiled);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = regex.Match(line);
            if (match.Success)
            {
                var name = match.Groups[2].Value;
                yield return (line, i + 1, name);
            }
        }
    }
}