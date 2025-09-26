using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using CodeReviewBot.Domain.Entities;
using CodeReviewBot.Domain.Interfaces;
using CodeReviewBot.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;
using Moq;

namespace CodeReviewBot.Performance.Tests.Scenarios;

[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net80)]
public class CodeAnalysisPerformanceTests
{
    private ICodeAnalyzer _codeAnalyzer = null!;
    private FileChange _largeFileChange = null!;
    private FileChange _smallFileChange = null!;

    [GlobalSetup]
    public void Setup()
    {
        var mockLogger = new Mock<ILogger<CodeAnalyzerService>>();
        _codeAnalyzer = new CodeAnalyzerService(mockLogger.Object);

        // Small file (typical class)
        _smallFileChange = new FileChange
        {
            Path = "SmallClass.cs",
            ChangeType = "edit",
            Content = GenerateSmallCSharpFile()
        };

        // Large file (complex class with many methods)
        _largeFileChange = new FileChange
        {
            Path = "LargeClass.cs",
            ChangeType = "edit",
            Content = GenerateLargeCSharpFile()
        };
    }

    [Benchmark]
    public async Task AnalyzeSmallFile()
    {
        await _codeAnalyzer.AnalyzeFileAsync(_smallFileChange);
    }

    [Benchmark]
    public async Task AnalyzeLargeFile()
    {
        await _codeAnalyzer.AnalyzeFileAsync(_largeFileChange);
    }

    [Benchmark]
    public async Task LoadCodingRules()
    {
        await _codeAnalyzer.LoadCodingRulesAsync();
    }

    private static string GenerateSmallCSharpFile()
    {
        return @"
using System;

namespace TestProject
{
    public class SmallClass
    {
        private readonly string _name;
        
        public SmallClass(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }
        
        public string GetName() => _name;
        
        public void DoSomething()
        {
            Console.WriteLine($""Hello {_name}"");
        }
    }
}";
    }

    private static string GenerateLargeCSharpFile()
    {
        var methods = new List<string>();

        // Generate 50 methods with various patterns
        for (int i = 0; i < 50; i++)
        {
            methods.Add($@"
        public async Task<bool> ProcessItem{i}Async(int id, string name, string description, string category, string status, string priority, string assignee, string reporter, DateTime createdDate, DateTime updatedDate)
        {{
            if (id <= 0)
            {{
                throw new ArgumentException(""Invalid ID"", nameof(id));
            }}
            
            try
            {{
                var result = await ValidateItem{i}Async(id);
                if (result)
                {{
                    var message = ""Processing item: "" + id + "" with name: "" + name + "" and description: "" + description;
                    Console.WriteLine(message);
                    
                    if (id > 1000)
                    {{
                        return true;
                    }}
                    
                    var query = ""SELECT * FROM Items WHERE Id = "" + id;
                    return await ExecuteQuery{i}Async(query);
                }}
                
                return false;
            }}
            catch (Exception ex)
            {{
                Console.WriteLine($""Error processing item {i}: {{ex.Message}}"");
                return false;
            }}
        }}
        
        private async Task<bool> ValidateItem{i}Async(int id)
        {{
            await Task.Delay(10);
            return id > 0;
        }}
        
        private async Task<bool> ExecuteQuery{i}Async(string query)
        {{
            await Task.Delay(5);
            return !string.IsNullOrEmpty(query);
        }}");
        }

        return $@"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestProject
{{
    public class LargeClass
    {{
        private readonly string _connectionString;
        private readonly int _maxRetries;
        
        public LargeClass(string connectionString, int maxRetries = 3)
        {{
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _maxRetries = maxRetries;
        }}
        
        {string.Join("\n", methods)}
        
        public void Dispose()
        {{
            // Cleanup resources
        }}
    }}
}}";
    }

    public static void RunBenchmarks()
    {
        BenchmarkRunner.Run<CodeAnalysisPerformanceTests>();
    }
}
