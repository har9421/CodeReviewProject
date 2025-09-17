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
            var ruleSet = GetLanguageRules(rules, "csharp");
            foreach (var rule in ruleSet)
            {
                var type = (string?)rule["type"];
                var pattern = (string?)rule["pattern"];
                var message = (string?)rule["message"];
                var severity = (string?)rule["severity"];
                var id = (string?)rule["id"];

                // Forbidden pattern check (simple substring)
                if (type == "forbidden" && !string.IsNullOrEmpty(pattern) && text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
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
                            Console.WriteLine($"Found property naming violation: {propName} in {file} at line {lineNumber}");
                            issues.Add(new CodeIssue
                            {
                                FilePath = file,
                                Line = lineNumber,
                                Message = message ?? "Property names should be in PascalCase.",
                                Severity = severity ?? "warning",
                                RuleId = id ?? "CS004"
                            });
                            Console.WriteLine($"Added CodeIssue for property {propName} with RuleId CS004");
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
                    foreach (var (lineText, lineNumber, methodName, isAsync) in FindMethodDeclarations(text))
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

                        // Async suffix check when method is async (CS008)
                        if (isAsync && (string.Equals(id, "CS008", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Async", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            if (!methodName.EndsWith("Async", StringComparison.Ordinal))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Async methods should end with 'Async' suffix.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS008"
                                });
                            }
                        }
                    }
                }

                // Field rules (constants and private field naming)
                if (appliesTo == "field_declaration")
                {
                    foreach (var (lineText, lineNumber, fieldName, isConst, isPrivate) in FindFieldDeclarations(text))
                    {
                        // Constants ALL_CAPS (CS005)
                        if (isConst && (string.Equals(id, "CS005", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Constants", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            if (!(fieldName.All(c => c == '_' || char.IsDigit(c) || char.IsUpper(c))))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Constants should be in ALL_CAPS with underscores.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS005"
                                });
                            }
                        }

                        // Private fields underscore camelCase (CS007)
                        if (isPrivate && !isConst && (string.Equals(id, "CS007", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Private fields", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            var valid = fieldName.StartsWith("_") && fieldName.Length > 1 && char.IsLower(fieldName[1]);
                            if (!valid)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Private fields should be in camelCase and prefixed with '_'.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS007"
                                });
                            }
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
            var ruleSet = GetLanguageRules(rules, "csharp");
            foreach (var rule in ruleSet)
            {
                var type = (string?)rule["type"];
                var pattern = (string?)rule["pattern"];
                var message = (string?)rule["message"];
                var severity = (string?)rule["severity"];
                var id = (string?)rule["id"];

                // Forbidden pattern check (simple substring)
                if (type == "forbidden" && !string.IsNullOrEmpty(pattern) && content.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
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
                    foreach (var (lineText, lineNumber, methodName, isAsync) in FindMethodDeclarations(content))
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

                        // Async suffix check when method is async (CS008)
                        if (isAsync && (string.Equals(id, "CS008", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Async", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            if (!methodName.EndsWith("Async", StringComparison.Ordinal))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = message ?? "Async methods should end with 'Async' suffix.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS008"
                                });
                            }
                        }
                    }
                }

                // Field rules (constants and private field naming)
                if (appliesTo == "field_declaration")
                {
                    foreach (var (lineText, lineNumber, fieldName, isConst, isPrivate) in FindFieldDeclarations(content))
                    {
                        // Constants ALL_CAPS (CS005)
                        if (isConst && (string.Equals(id, "CS005", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Constants", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            if (!(fieldName.All(c => c == '_' || char.IsDigit(c) || char.IsUpper(c))))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = message ?? "Constants should be in ALL_CAPS with underscores.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS005"
                                });
                            }
                        }

                        // Private fields underscore camelCase (CS007)
                        if (isPrivate && !isConst && (string.Equals(id, "CS007", StringComparison.OrdinalIgnoreCase) || (message?.Contains("Private fields", StringComparison.OrdinalIgnoreCase) ?? false)))
                        {
                            var valid = fieldName.StartsWith("_") && fieldName.Length > 1 && char.IsLower(fieldName[1]);
                            if (!valid)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = message ?? "Private fields should be in camelCase and prefixed with '_'.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS007"
                                });
                            }
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

    private JArray GetLanguageRules(JObject rulesRoot, string language)
    {
        // Preferred schema: { "csharp": { "rules": [ ... ] } }
        var direct = rulesRoot[language]?["rules"] as JArray;
        if (direct != null)
            return direct;

        // Fallback schema: { "rules": [ { languages: ["csharp"], ... } ] }
        var top = rulesRoot["rules"] as JArray;
        if (top != null)
        {
            var filtered = new JArray();
            foreach (var r in top)
            {
                var langs = r["languages"] as JArray;
                if (langs != null && langs.Any(l => string.Equals((string?)l, language, StringComparison.OrdinalIgnoreCase)))
                {
                    filtered.Add(r);
                }
            }
            return filtered;
        }

        return new JArray();
    }

    private IEnumerable<(string lineText, int lineNumber, string propertyName)> FindPropertyDeclarations(string content)
    {
        // Match properties with get/set/init in any format, including attributes and spacing
        // Examples:
        // public string role { get; set; }
        // public string Role { get; init; }
        // [Attr]\npublic int Count { get { return _c; } set { _c = value; } }
        var regex = new Regex(
            @"(?ms)^[\t ]*(?:\[[^\]]*\][\t ]*)*(public|protected|internal|private)[\t ]+(?:static[\t ]+)?[\w<>\?\[\],\t ]+[\t ]+(\w+)[\t ]*\{[^}]*\b(get|set|init)\b[\s\S]*?\}",
            RegexOptions.Compiled);

        foreach (Match match in regex.Matches(content))
        {
            var name = match.Groups[2].Value;
            var lineNumber = GetLineNumberByIndex(content, match.Index);
            var lineText = GetLineTextAtIndex(content, match.Index);
            yield return (lineText, lineNumber, name);
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

    private IEnumerable<(string lineText, int lineNumber, string methodName, bool isAsync)> FindMethodDeclarations(string content)
    {
        // Matches methods like: public void DoWork(..., with optional generics/return types)
        var regex = new Regex(@"\b(public|protected|internal|private)\s+(async\s+)?[\w<>\?\[\]]+\s+(\w+)\s*\(", RegexOptions.Compiled);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = regex.Match(line);
            if (match.Success)
            {
                var isAsync = !string.IsNullOrEmpty(match.Groups[2].Value);
                var name = match.Groups[3].Value;
                yield return (line, i + 1, name, isAsync);
            }
        }
    }

    private IEnumerable<(string lineText, int lineNumber, string fieldName, bool isConst, bool isPrivate)> FindFieldDeclarations(string content)
    {
        // Matches fields like: private const int MAX; private string _name; public static readonly int X;
        var regex = new Regex(@"\b(public|protected|internal|private)\s+((?:static|readonly|const)\s+)*[\w<>\?\[\]]+\s+(\w+)\s*;", RegexOptions.Compiled);
        var lines = content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = regex.Match(line);
            if (match.Success)
            {
                var access = match.Groups[1].Value;
                var mods = match.Groups[2].Value;
                var name = match.Groups[3].Value;
                var isConst = mods?.Contains("const") == true;
                var isPrivate = string.Equals(access, "private", StringComparison.OrdinalIgnoreCase);
                yield return (line, i + 1, name, isConst, isPrivate);
            }
        }
    }

    private int GetLineNumberByIndex(string text, int index)
    {
        if (index <= 0) return 1;
        var segment = index >= text.Length ? text : text.Substring(0, index);
        return segment.Split('\n').Length;
    }

    private string GetLineTextAtIndex(string text, int index)
    {
        var start = text.LastIndexOf('\n', index >= text.Length ? text.Length - 1 : index);
        var end = text.IndexOf('\n', index);
        if (start < 0) start = -1;
        if (end < 0) end = text.Length;
        return text.Substring(start + 1, end - start - 1);
    }
}