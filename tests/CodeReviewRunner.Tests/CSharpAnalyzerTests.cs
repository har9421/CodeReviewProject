using Xunit;
using CodeReviewRunner.Services;
using Newtonsoft.Json.Linq;
using System.IO;
using System;

namespace CodeReviewRunner.Tests;

public class CSharpAnalyzerTests
{
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

    var analyzer = new CSharpAnalyzer();
    var issues = analyzer.Analyze(tempDir, rules);

    Assert.Single(issues);
    Assert.Equal("CS001", issues[0].RuleId);
  }
}