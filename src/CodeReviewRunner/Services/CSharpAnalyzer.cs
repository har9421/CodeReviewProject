using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;
using System.Text.RegularExpressions;

namespace CodeReviewRunner.Services
{
    public class CSharpAnalyzer
    {
        // Matches method declarations with modifiers, return type, and name
        private static readonly Regex MethodDeclarationRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches type declarations (class, interface, struct, record) - allow any starting case to validate later
        private static readonly Regex TypeDeclarationRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:abstract\s+|sealed\s+)?(class|interface|struct|record)\s+([A-Za-z_]\w*)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches method parameters
        private static readonly Regex ParameterDeclarationRegex = new(
            @"(?<=\(|,)\s*(?:ref\s+|out\s+|in\s+|params\s+)?[\w<>\[\],\s]+?\s+([A-Za-z_]\w*)\s*(?:=.*?)?(?=,|\)|$)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches property declarations with modifiers and type - allow any starting case to validate later
        private static readonly Regex PropertyDeclarationRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*[\w<>\[\],\s]+\s+([A-Za-z_]\w*)\s*\{",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches field declarations with modifiers and type
        private static readonly Regex FieldDeclarationRegex = new(
            @"^\s*(public|protected|internal|private)\s+((?:static|readonly|const)\s+)*[\w<>\?\[\]]+\s+(_?[a-zA-Z]\w*)\s*(?:;|=)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Additional patterns for specific rules
        private static readonly Regex InterfaceNameRegex = new(
            @"^\s*(?:public|internal)?\s*interface\s+(?!I[A-Z]\w*\b)(\w+)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex ConstantFieldRegex = new(
            @"^\s*(?:public\s+|private\s+|protected\s+|internal\s+)?(?:static\s+)?const\s+[\w<>\[\],\s]+\s+([^A-Z_]\w*)\b",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex AsyncMethodWithoutAsyncSuffixRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:\w+\s+)*async\s+[\w<>\[\],\s]+\s+(\w+)(?<!Async)\s*\(",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Local variable declarations (inside methods/blocks)
        private static readonly Regex LocalVariableDeclarationRegex = new(
            @"^\s*(?:var|[A-Za-z_][A-Za-z0-9_<>\[\]]*)\s+([a-zA-Z_][A-Za-z0-9_]*)\s*(?::\s*[\w<>\[\]?]+)?\s*(=|;)",
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

                    var appliesTo = (string?)rule["applies_to"] ?? (string?)rule["appliesTo"] ?? (string?)rule["target"] ?? string.Empty;

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

                    if (appliesTo == "type_declaration")
                    {
                        foreach (var (lineText, lineNumber, typeName) in FindTypeDeclarations(text))
                        {
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

                    if (appliesTo == "method_declaration")
                    {
                        foreach (var (lineText, lineNumber, methodName, isAsync) in FindMethodDeclarations(text))
                        {
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

                    if (appliesTo == "field_declaration")
                    {
                        foreach (var (lineText, lineNumber, fieldName, isConst, isPrivate, isPublic) in FindFieldDeclarations(text))
                        {
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

                            if (isPublic)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Avoid public fields; use properties instead.",
                                    Severity = severity ?? "warning",
                                    RuleId = "CS010"
                                });
                            }

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

                    if (appliesTo == "variable_declaration" || string.Equals(id, "unused-variable", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var (lineText, lineNumber, variableName) in FindLocalVariableDeclarations(text))
                        {
                            var usagePattern = new Regex($"\\b{Regex.Escape(variableName)}\\b", RegexOptions.Multiline);
                            var matches = usagePattern.Matches(text);
                            if (matches.Count <= 1)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = file,
                                    Line = lineNumber,
                                    Message = message ?? "Unused variable detected",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "unused-variable"
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
                var ruleSet = GetLanguageRules(rules, "csharp");
                foreach (var rule in ruleSet)
                {
                    var type = (string?)rule["type"];
                    var pattern = (string?)rule["pattern"];
                    var message = (string?)rule["message"];
                    var severity = (string?)rule["severity"];
                    var id = (string?)rule["id"];

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

                    var appliesTo = (string?)rule["applies_to"] ?? (string?)rule["appliesTo"] ?? (string?)rule["target"] ?? string.Empty;
                    if (appliesTo == "parameter_declaration")
                    {
                        foreach (var (lineText, lineNumber, paramName) in FindParameterDeclarations(content))
                        {
                            if (!string.IsNullOrEmpty(paramName))
                            {
                                if (paramName.StartsWith("_"))
                                {
                                    issues.Add(new CodeIssue
                                    {
                                        FilePath = path,
                                        Line = lineNumber,
                                        Message = message ?? "Parameters must be in camelCase.",
                                        Severity = severity ?? "warning",
                                        RuleId = id ?? "CS012",
                                        Description = $"Parameter '{paramName}' should not start with underscore."
                                    });
                                }
                                else if (!char.IsLower(paramName[0]))
                                {
                                    issues.Add(new CodeIssue
                                    {
                                        FilePath = path,
                                        Line = lineNumber,
                                        Message = message ?? "Parameters must be in camelCase.",
                                        Severity = severity ?? "warning",
                                        RuleId = id ?? "CS012",
                                        Description = $"Parameter '{paramName}' should start with a lowercase letter."
                                    });
                                }
                            }
                        }
                    }

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

                            var match = InterfaceNameRegex.Match(lineText);
                            if (match.Success)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = "Interface names must start with 'I'.",
                                    Severity = "warning",
                                    RuleId = "CS009"
                                });
                            }
                        }
                    }

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

                    if (appliesTo == "field_declaration")
                    {
                        foreach (var (lineText, lineNumber, fieldName, isConst, isPrivate, isPublic) in FindFieldDeclarations(content))
                        {
                            if (isConst)
                            {
                                if (!(fieldName.All(c => c == '_' || char.IsDigit(c) || char.IsUpper(c))))
                                {
                                    issues.Add(new CodeIssue
                                    {
                                        FilePath = path,
                                        Line = lineNumber,
                                        Message = message ?? "Constants should be in ALL_CAPS with underscores.",
                                        Severity = severity ?? "warning",
                                        RuleId = "CS005"
                                    });
                                }
                            }

                            if (isPublic)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = message ?? "Avoid public fields; use properties instead.",
                                    Severity = severity ?? "warning",
                                    RuleId = "CS010"
                                });
                            }

                            if (isPrivate && !isConst)
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

                    if (appliesTo == "variable_declaration" || string.Equals(id, "unused-variable", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var (lineText, lineNumber, variableName) in FindLocalVariableDeclarations(content))
                        {
                            var usagePattern = new Regex($"\\b{Regex.Escape(variableName)}\\b", RegexOptions.Multiline);
                            var matches = usagePattern.Matches(content);
                            if (matches.Count <= 1)
                            {
                                issues.Add(new CodeIssue
                                {
                                    FilePath = path,
                                    Line = lineNumber,
                                    Message = message ?? "Unused variable detected",
                                    Severity = severity ?? "warning",
                                    RuleId = id ?? "unused-variable"
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

        private JArray GetLanguageRules(JObject rulesRoot, string language)
        {
            var direct = rulesRoot[language]?["rules"] as JArray;
            if (direct != null)
                return direct;

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
                var name = match.Groups[1].Value;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var lineText = GetLineTextAtIndex(content, match.Index);

                yield return (lineText, lineNumber, name);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string typeName)> FindTypeDeclarations(string content)
        {
            foreach (Match match in TypeDeclarationRegex.Matches(content))
            {
                var typeName = match.Groups[2].Value;
                if (string.IsNullOrEmpty(typeName))
                {
                    continue;
                }

                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var lineText = GetLineTextAtIndex(content, match.Index);

                yield return (lineText, lineNumber, typeName);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string methodName, bool isAsync)> FindMethodDeclarations(string content)
        {
            foreach (Match match in MethodDeclarationRegex.Matches(content))
            {
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var methodName = match.Groups[2].Value;
                var isAsync = !string.IsNullOrEmpty(match.Groups[1].Value);

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

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

        private IEnumerable<(string lineText, int lineNumber, string fieldName, bool isConst, bool isPrivate, bool isPublic)> FindFieldDeclarations(string content)
        {
            foreach (Match match in FieldDeclarationRegex.Matches(content))
            {
                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var access = match.Groups[1].Value;
                var mods = match.Groups[2].Value;
                var name = match.Groups[3].Value;
                var isConst = mods.Contains("const", StringComparison.OrdinalIgnoreCase);
                var isPrivate = string.Equals(access, "private", StringComparison.OrdinalIgnoreCase);
                var isPublic = string.Equals(access, "public", StringComparison.OrdinalIgnoreCase);

                var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                var lineEnd = content.IndexOf('\n', match.Index);
                if (lineEnd == -1) lineEnd = content.Length;
                var lineText = content.Substring(lineStart, lineEnd - lineStart);

                yield return (lineText, lineNumber, name, isConst, isPrivate, isPublic);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string variableName)> FindLocalVariableDeclarations(string content)
        {
            foreach (Match match in LocalVariableDeclarationRegex.Matches(content))
            {
                var varName = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(varName))
                {
                    continue;
                }

                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var lineText = GetLineTextAtIndex(content, match.Index);

                if (Regex.IsMatch(lineText, @"\b(public|private|protected|internal)\b"))
                {
                    continue;
                }

                yield return (lineText, lineNumber, varName);
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string parameterName)> FindParameterDeclarations(string content)
        {
            foreach (Match match in ParameterDeclarationRegex.Matches(content))
            {
                var parameterName = match.Groups[1].Value;
                if (string.IsNullOrEmpty(parameterName))
                {
                    Console.WriteLine($"Found empty parameter match - skipping");
                    continue;
                }

                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var matchLine = GetLineTextAtIndex(content, match.Index);

                Console.WriteLine($"Found parameter: {parameterName} at line {lineNumber} in: {matchLine.Trim()}");
                yield return (matchLine, lineNumber, parameterName);
            }
        }

        private string GetLineTextAtIndex(string text, int index)
        {
            try
            {
                var lineStart = text.LastIndexOf('\n', Math.Min(index, text.Length - 1));
                if (lineStart < 0) lineStart = -1;

                var lineEnd = index < text.Length ? text.IndexOf('\n', index) : -1;
                if (lineEnd < 0) lineEnd = text.Length;

                return text.Substring(lineStart + 1, lineEnd - lineStart - 1);
            }
            catch
            {
                Console.WriteLine($"Error getting line text at index {index} from text length {text.Length}");
                return string.Empty;
            }
        }
    }
}