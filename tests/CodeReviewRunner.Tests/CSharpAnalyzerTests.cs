using Xunit;
using CodeReviewRunner.Services;
using Newtonsoft.Json.Linq;
using System.IO;
using System;

namespace CodeReviewRunner.Tests;

public class CSharpAnalyzerTests
{
  private readonly CSharpAnalyzer _analyzer;

  public CSharpAnalyzerTests()
  {
    _analyzer = new CSharpAnalyzer();
  }

  [Fact]
  public void DetectsForbiddenPattern()
  {
    var rules = JObject.Parse(@"{
            'csharp': { 'rules': [
                { 'id':'CS001', 'type':'forbidden', 'pattern':'Console.WriteLine',
                  'message':'Avoid Console.WriteLine', 'severity':'error' }
            ]}
        }");

    var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;
    var tempFile = Path.Combine(tempDir, "Test.cs");
    File.WriteAllText(tempFile, "class Test { void Run() { Console.WriteLine(\"Hello\"); } }");

    var issues = _analyzer.Analyze(tempDir, rules);

    Assert.Single(issues);
    Assert.Equal("CS001", issues[0].RuleId);
  }

  [Fact]
  public void DetectsTypePascalCaseViolation()
  {
    var rules = CreateRuleSet("type_declaration");
    var code = @"
            namespace Test {
                public class lowercaseClass { }
                public interface nonIInterface { }
                public class ValidClass { }
                public interface IValidInterface { }
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Equal(2, issues.Count);
    Assert.Contains(issues, i => i.RuleId == "CS001" && i.Line == 3); // lowercaseClass
    Assert.Contains(issues, i => i.RuleId == "CS009" && i.Line == 4); // nonIInterface
  }

  [Fact]
  public void DetectsAsyncMethodViolation()
  {
    var rules = CreateRuleSet("method_declaration");
    var code = @"
            public class Test {
                public async Task DoSomething() { }
                public async Task<int> GetValueAsync() { }
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Single(issues);
    var issue = Assert.Single(issues, i => i.RuleId == "CS008");
    Assert.Contains("Async suffix", issue.Message);
    Assert.Equal(3, issue.Line); // DoSomething method
  }

  [Fact]
  public void DetectsPrivateFieldViolations()
  {
    var rules = CreateRuleSet("field_declaration");
    var code = @"
            public class Test {
                private string wrongName;
                private string _validName;
                private const string invalid_constant = ""test"";
                private const string VALID_CONSTANT = ""test"";
                public string publicField;
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Equal(3, issues.Count);
    Assert.Contains(issues, i => i.RuleId == "CS007" && i.Line == 3); // wrongName
    Assert.Contains(issues, i => i.RuleId == "CS005" && i.Line == 5); // invalid_constant
    Assert.Contains(issues, i => i.RuleId == "CS010" && i.Line == 7); // publicField
  }

  [Fact]
  public void DetectsPropertyPascalCaseViolation()
  {
    var rules = CreateRuleSet("property_declaration");
    var code = @"
            public class Test {
                public string invalidName { get; set; }
                public string ValidName { get; set; }
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Single(issues);
    var issue = Assert.Single(issues, i => i.RuleId == "CS004");
    Assert.Equal(3, issue.Line);
    Assert.Contains("PascalCase", issue.Message);
  }

  [Fact]
  public void DetectsGenericTypeViolations()
  {
    var rules = CreateRuleSet("type_declaration");
    var code = @"
            public class dataContainer<T> { }
            public class DataContainer<TValue> { }
            public interface genericInterface<T> { }
            public interface IGenericInterface<TItem> { }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Equal(2, issues.Count);
    Assert.Contains(issues, i => i.RuleId == "CS001" && i.Message.Contains("PascalCase") && i.Line == 2);
    Assert.Contains(issues, i => i.RuleId == "CS009" && i.Message.Contains("Interface") && i.Line == 4);
  }

  [Fact]
  public void DetectsAsyncOverloadViolations()
  {
    var rules = CreateRuleSet("method_declaration");
    var code = @"
            public class Service {
                public string GetData(int id) { return """"; }
                public async Task<string> GetData(int id, bool refresh) { return """"; }
                public async Task<string> GetDataAsync(int id) { return """"; }
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Single(issues);
    var issue = Assert.Single(issues, i => i.RuleId == "CS008");
    Assert.Equal(4, issue.Line); // The async overload without Async suffix
  }

  [Fact]
  public void DetectsStaticMemberViolations()
  {
    var rules = CreateRuleSet("field_declaration");
    var code = @"
            public class Utility {
                private static string defaultValue;
                private static string _defaultConfig;
                public static string SharedState;
                private static readonly string _DEFAULT_CONNECTION = ""local"";
                private static readonly string invalidConstant = ""test"";
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Equal(3, issues.Count);
    Assert.Contains(issues, i => i.RuleId == "CS007" && i.Line == 3); // Missing underscore
    Assert.Contains(issues, i => i.RuleId == "CS010" && i.Line == 5); // Public field
    Assert.Contains(issues, i => i.RuleId == "CS007" && i.Line == 7); // Invalid readonly naming
  }

  [Fact]
  public void DetectsParameterNamingViolations()
  {
    var rules = CreateRuleSet("parameter_declaration");
    var code = @"
            public class Service {
                public void ProcessData(
                    string ID,
                    string UserName,
                    int _count,
                    string validName)
                { }
            }";

    var issues = _analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

    Assert.Equal(3, issues.Count);
    Assert.Contains(issues, i => i.Message.Contains("camelCase") && i.Line == 4);  // ID
    Assert.Contains(issues, i => i.Message.Contains("camelCase") && i.Line == 5);  // UserName
    Assert.Contains(issues, i => i.Message.Contains("underscore") && i.Line == 6); // _count
  }

  private static JObject CreateRuleSet(string appliesTo)
  {
    return JObject.Parse($@"{{
            'csharp': {{ 'rules': [
                {{
                    'id': 'test',
                    'type': 'style',
                    'applies_to': '{appliesTo}',
                    'message': 'Style check',
                    'severity': 'warning'
                }}
            ]}}
        }}");
  }
}