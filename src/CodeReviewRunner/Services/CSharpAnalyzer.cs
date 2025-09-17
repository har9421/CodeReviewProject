using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;
using System.Text.RegularExpressions;

namespace CodeReviewRunner.Services
{
    public class CSharpAnalyzer
    {
        // Matches method declarations with modifiers, return type, and name
        private static readonly Regex MethodDeclarationRegex = new(
            @"^\s*(public|private|protected|internal)\s+(virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches type declarations (class, interface, struct, record)
        private static readonly Regex TypeDeclarationRegex = new(
            @"^\s*(public|private|protected|internal)\s+(abstract\s+|sealed\s+)*(class|interface|struct|record)\s+([A-Z]\w*)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches method parameters
        private static readonly Regex ParameterDeclarationRegex = new(
            @"(?<=\(|,)\s*(ref\s+|out\s+|in\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*(?=,|\))",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches property declarations with modifiers and type
        private static readonly Regex PropertyDeclarationRegex = new(
            @"^\s*(public|private|protected|internal)\s+(virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*[\w<>\[\],\s]+\s+([A-Z]\w*)\s*\{",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches field declarations with modifiers and type
        private static readonly Regex FieldDeclarationRegex = new(
            @"^\s*(public|protected|internal|private)\s+((?:static|readonly|const)\s+)*[\w<>\?\[\]]+\s+(_?[a-zA-Z]\w*)\s*(?:;|=)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Additional patterns for specific rules
        private static readonly Regex InterfaceNameRegex = new(
            @"^\s*(?:public|internal)\s+interface\s+(?!I[A-Z]\w*\b)(\w+)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex ConstantFieldRegex = new(
            @"^\s*(?:public\s+|private\s+|protected\s+|internal\s+)(?:static\s+)?const\s+[\w<>\[\],\s]+\s+([^A-Z_]\w*)\b",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex AsyncMethodWithoutAsyncSuffixRegex = new(
            @"^\s*(?:public|private|protected|internal)\s+(?:\w+\s+)*async\s+[\w<>\[\],\s]+\s+(\w+)(?<!Async)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);



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
                        IgnoreInaccessible = true
                    });
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

                    var appliesTo = (string?)rule["applies_to"];
                    var ruleType = (string?)rule["type"];

                    // Parameter naming check: enforce camelCase
                    if (appliesTo == "parameter_declaration")
                    {
                        foreach (var (lineText, lineNumber, paramName) in FindParameterDeclarations(text))
                        {
                            // Check for underscore prefix (not allowed in parameters)
                            if (paramName.StartsWith("_"))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = "Parameters should not start with underscore.",
                                    Severity = "warning",
                                    RuleId = "CS011",
                                    Description = $"Parameter '{paramName}' should not start with underscore."
                                });
                            }
                            // Check for PascalCase (should be camelCase)
                            else if (char.IsUpper(paramName[0]))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = "Parameters should be in camelCase.",
                                    Severity = "warning",
                                    RuleId = "CS012",
                                    Description = $"Parameter '{paramName}' should start with lowercase letter."
                                });
                            }
                        }
                    }

                    // Property naming convention check
                    if (appliesTo == "property_declaration")
                    {
                        foreach (var (lineText, lineNumber, propName) in FindPropertyDeclarations(text))
                        {
                            if (string.IsNullOrEmpty(propName) || !char.IsUpper(propName[0]))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Property names must be in PascalCase.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS004",
                                    Description = $"Property '{propName}' should start with an uppercase letter."
                                });
                            }
                        }
                    }

                    // Type naming convention check
                    if (appliesTo == "type_declaration")
                    {
                        foreach (var (lineText, lineNumber, typeName) in FindTypeDeclarations(text))
                        {
                            // PascalCase check for all types
                            if (string.IsNullOrEmpty(typeName) || !char.IsUpper(typeName[0]))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Type names must be in PascalCase.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS001",
                                    Description = $"Type '{typeName}' should start with an uppercase letter."
                                });
                            }

                            // Interface 'I' prefix check
                            var match = InterfaceNameRegex.Match(lineText);
                            if (match.Success)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = "Interface names must start with 'I'.",
                                    Severity = "warning",
                                    RuleId = "CS009",
                                    Description = $"Interface '{match.Groups[1].Value}' should be renamed to 'I{match.Groups[1].Value}'."
                                });
                            }
                        }
                    }

                    // Method naming convention check
                    if (appliesTo == "method_declaration")
                    {
                        foreach (var (lineText, lineNumber, methodName, isAsync) in FindMethodDeclarations(text))
                        {
                            // PascalCase check for methods
                            if (string.IsNullOrEmpty(methodName) || !char.IsUpper(methodName[0]))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Method names must be in PascalCase.",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "CS002",
                                    Description = $"Method '{methodName}' should start with an uppercase letter."
                                });
                            }

                            // Async suffix check
                            if (isAsync && !methodName.EndsWith("Async", StringComparison.Ordinal))
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Async methods must end with 'Async' suffix.",
                                    Severity = severity ?? "warning",
                                    RuleId = "CS008",
                                    Description = $"Async method '{methodName}' should be renamed to '{methodName}Async'."
                                });
                            }
                        }
                    }

                    // Field naming convention check
                    if (appliesTo == "field_declaration")
                    {
                        foreach (var (lineText, lineNumber, fieldName, isConst, isPrivate) in FindFieldDeclarations(text))
                        {
                            // Constants in ALL_CAPS
                            if (isConst)
                            {
                                var isValid = fieldName.All(c => c == '_' || char.IsDigit(c) || char.IsUpper(c));
                                if (!isValid)
                                {
                                    issues.Add(new CodeIssue
                                    {
                                        FilePath = file,
                                        Line = lineNumber,
                                        Message = message ?? "Constants must be in ALL_CAPS with underscores.",
                                        Severity = severity ?? "warning",
                                        RuleId = "CS005",
                                        Description = $"Constant '{fieldName}' should be in uppercase with underscores."
                                    });
                                }
                            }

                            // Private fields with underscore prefix and camelCase
                            if (isPrivate && !isConst)
                            {
                                var isValid = fieldName.StartsWith("_") && fieldName.Length > 1 && char.IsLower(fieldName[1]);
                                if (!isValid)
                                {
                                    issues.Add(new CodeIssue
                                    {
                                        FilePath = file,
                                        Line = lineNumber,
                                        Message = message ?? "Private fields must be in camelCase and prefixed with '_'.",
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
            foreach (Match match in PropertyDeclarationRegex.Matches(content))
            {
                var name = match.Groups[3].Value;
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

                yield return (lineText, lineNumber, name);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string typeName)> FindTypeDeclarations(string content)
        {
            foreach (Match match in TypeDeclarationRegex.Matches(content))
            {
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var typeName = match.Groups[4].Value;

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);
                yield return (lineText, lineNumber, typeName);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string methodName, bool isAsync)> FindMethodDeclarations(string content)
        {
            foreach (Match match in MethodDeclarationRegex.Matches(content))
            {
                // Calculate the line number by counting newlines before this match
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var methodName = match.Groups[4].Value;
                var isAsync = !string.IsNullOrEmpty(match.Groups[3].Value);

                // Get the full line text
                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

                // Filter out non-method declarations by checking the line content
                if (!lineText.Contains("class") &&
                    !lineText.Contains("interface") &&
                    !lineText.Contains("struct"))
                {
                    if (isAsync)
                    {
                        Console.WriteLine($"Found async method: {methodName} at line {lineNumber}");
                    }

                    yield return (lineText, lineNumber, methodName, isAsync);
                }
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string fieldName, bool isConst, bool isPrivate)> FindFieldDeclarations(string content)
        {
            foreach (Match match in FieldDeclarationRegex.Matches(content))
            {
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var access = match.Groups[1].Value;
                var mods = match.Groups[2].Value;
                var name = match.Groups[3].Value;
                var isConst = mods.Contains("const", StringComparison.OrdinalIgnoreCase);
                var isPrivate = string.Equals(access, "private", StringComparison.OrdinalIgnoreCase);

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

                yield return (lineText, lineNumber, name, isConst, isPrivate);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string parameterName)> FindParameterDeclarations(string content)
        {
            foreach (Match match in ParameterDeclarationRegex.Matches(content))
            {
                var parameterName = match.Groups[2].Value;
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

                yield return (lineText, lineNumber, parameterName);
            }
        }

        private int GetLineNumberByIndex(string text, int index)
        {
            if (index <= 0) return 1;
            return text.Take(index).Count(c => c == '\n') + 1;
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
}