using Newtonsoft.Json.Linq;
using CodeReviewRunner.Models;
using System.Text.RegularExpressions;

namespace CodeReviewRunner.Services
{
    public class CSharpAnalyzer
    {
        // Matches method declarations with modifiers, return type, and name
        private static readonly Regex MethodDeclarationRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\([^)]*\)\s*(?:\{|;|$)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Matches type declarations (class, interface, struct, record) - allow any starting case to validate later
        private static readonly Regex TypeDeclarationRegex = new(
            @"^\s*(?:public|private|protected|internal)?\s*(?:abstract\s+|sealed\s+)?(class|interface|struct|record)\s+([A-Za-z_]\w*)",
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

        // Local variable declarations (inside methods/blocks)
        private static readonly Regex LocalVariableDeclarationRegex = new(
            @"^\s*(?:var|[A-Za-z_][A-Za-z0-9_<>\[\]]*)\s+([a-zA-Z_][A-Za-z0-9_]*)\s*(?::\s*[\w<>\[\]?]+)?\s*(=|;)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        // Method parameters
        private static readonly Regex ParameterDeclarationRegex = new(
            @"(?<=\(|,)\s*(?:ref\s+|out\s+|in\s+|params\s+)?[\w<>\[\],\s]+?\s+([A-Za-z_]\w*)\s*(?:=.*?)?(?=,|\)|$)",
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
                    new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true });
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
                    var appliesTo = (string?)rule["applies_to"] ?? (string?)rule["appliesTo"] ?? (string?)rule["target"] ?? string.Empty;

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
                        continue;
                    }

                    switch (appliesTo)
                    {
                        case "method_declaration":
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
                                        Description = $"Method '{methodName}' should start with an uppercase letter.",
                                        LineText = lineText
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
                                        Description = $"Async method '{methodName}' should be renamed to '{methodName}Async'.",
                                        LineText = lineText,
                                        Suggestion = lineText?.Replace(methodName, methodName + "Async")
                                    });
                                }
                            }
                            break;

                        case "variable_declaration":
                        case "unused-variable":
                            foreach (var (lineText, lineNumber, variableName) in FindLocalVariableDeclarations(text))
                            {
                                // Find method scope bounds
                                var methodStart = text.LastIndexOf('{', Math.Min(text.Length - 1, text.Take(lineNumber).Count()));
                                var methodEnd = methodStart >= 0 ? FindMatchingBrace(text, methodStart) : text.Length;

                                if (methodStart >= 0 && methodEnd > methodStart)
                                {
                                    var scope = text.Substring(methodStart, methodEnd - methodStart);

                                    // Create more precise pattern that excludes variable declaration
                                    var declarationPattern = new Regex($@"(?:var|[A-Za-z_][A-Za-z0-9_<>\[\]]*)\s+{Regex.Escape(variableName)}\s*(?::|=|;)");
                                    var usagePattern = new Regex($@"\b{Regex.Escape(variableName)}\b(?!\s*(?::|=|;|\)))");

                                    var declarations = declarationPattern.Matches(scope);
                                    var usages = usagePattern.Matches(scope);

                                    // If we only find the declaration and no other usages, mark as unused
                                    if (declarations.Count > 0 && (usages.Count == 0 ||
                                        (usages.Count == 1 && usages[0].Index <= declarations[0].Index + declarations[0].Length)))
                                    {
                                        issues.Add(new CodeIssue
                                        {
                                            FilePath = file,
                                            Line = lineNumber,
                                            Message = message ?? "Variable is declared but never used",
                                            Severity = severity ?? "warning",
                                            RuleId = id ?? "unused-variable",
                                            Description = $"The variable '{variableName}' is declared but never used in the code.",
                                            LineText = lineText,
                                            Suggestion = "// Remove this unused variable declaration"
                                        });
                                    }
                                }
                            }
                            break;
                    }
                }
            }
            return issues;
        }

        private int FindMatchingBrace(string text, int openBraceIndex)
        {
            if (openBraceIndex < 0 || openBraceIndex >= text.Length || text[openBraceIndex] != '{')
                return text.Length;

            int depth = 1;
            for (int i = openBraceIndex + 1; i < text.Length; i++)
            {
                if (text[i] == '{')
                    depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return text.Length;
        }

        private int GetLineNumber(string text, string pattern)
        {
            var index = text.IndexOf(pattern);
            return index < 0 ? 1 : text.Substring(0, index).Split('\n').Length;
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

                var lineText = GetLineTextAtIndex(content, match.Index);

                if (!lineText.Contains("class") &&
                    !lineText.Contains("interface") &&
                    !lineText.Contains("struct"))
                {
                    yield return (lineText, lineNumber, methodName, isAsync);
                }
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

                if (!Regex.IsMatch(lineText, @"\b(public|private|protected|internal)\b"))
                {
                    yield return (lineText, lineNumber, varName);
                }
            }
        }

        private IEnumerable<(string lineText, int lineNumber, string parameterName)> FindParameterDeclarations(string content)
        {
            foreach (Match match in ParameterDeclarationRegex.Matches(content))
            {
                var parameterName = match.Groups[1].Value;
                if (string.IsNullOrEmpty(parameterName))
                {
                    continue;
                }

                var lineNumber = content.Take(match.Index).Count(c => c == '\n') + 1;
                var lineText = GetLineTextAtIndex(content, match.Index);
                yield return (lineText, lineNumber, parameterName);
            }
        }

        public List<CodeIssue> AnalyzeFromContent(JObject rules, IEnumerable<(string path, string content)> files)
        {
            var issues = new List<CodeIssue>();

            foreach (var (path, content) in files)
            {
                Console.WriteLine($"  Analyzing content for: {path}");
                var ruleSet = GetLanguageRules(rules, "csharp");

                foreach (var rule in ruleSet)
                {
                    var type = (string?)rule["type"];
                    var pattern = (string?)rule["pattern"];
                    var message = (string?)rule["message"];
                    var severity = (string?)rule["severity"];
                    var id = (string?)rule["id"];
                    var appliesTo = (string?)rule["applies_to"] ?? (string?)rule["appliesTo"] ?? (string?)rule["target"] ?? string.Empty;

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
                        continue;
                    }

                }
            }
            return issues;
        }
    }
}