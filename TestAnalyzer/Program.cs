using CodeReviewRunner.Services;
using Newtonsoft.Json.Linq;

var analyzer = new CSharpAnalyzer();
var rules = JObject.Parse(@"{
    'csharp': { 'rules': [
        { 'id':'CS001', 'type':'style', 'applies_to':'type_declaration', 'message':'Type names must be in PascalCase.', 'severity':'warning' }
    ]}
}");

var code = @"
public class dataContainer<T> { }
public class DataContainer<TValue> { }
public interface genericInterface<T> { }
public interface IGenericInterface<TItem> { }";

var issues = analyzer.AnalyzeFromContent(rules, new[] { ("test.cs", code) });

Console.WriteLine($"Found {issues.Count} issues:");
foreach (var issue in issues)
{
    Console.WriteLine($"  {issue.RuleId} at line {issue.Line}: {issue.Message}");
    Console.WriteLine($"    Description: {issue.Description}");
}
