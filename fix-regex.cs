using System;
using System.Text.RegularExpressions;

// Test different regex patterns
class Program
{
    static void Main()
    {
        var content = @"public async Task<User> Login(User user)
{
    return null;
}

public async Task Register(User user)
{
    var test = ""fdsre"";
}

public void SomeMethod()
{
    if (app.Environment.IsDevelopment())
    {
        // This should not be flagged as a method
    }
}";

        // Current pattern (too restrictive)
        var currentPattern = @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\([^)]*\)\s*(?:\{|;|$)";

        // Simpler pattern that should work
        var simplePattern = @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\([^)]*\)";

        Console.WriteLine("Testing current pattern:");
        TestPattern(content, currentPattern);

        Console.WriteLine("\nTesting simple pattern:");
        TestPattern(content, simplePattern);
    }

    static void TestPattern(string content, string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Multiline);
        var matches = regex.Matches(content);
        Console.WriteLine($"Found {matches.Count} matches:");

        foreach (Match match in matches)
        {
            Console.WriteLine($"Method: '{match.Groups[2].Value}', IsAsync: {!string.IsNullOrEmpty(match.Groups[1].Value)}");
        }
    }
}
