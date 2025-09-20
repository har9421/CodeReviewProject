using System;
using System.Text.RegularExpressions;

var content = @"public async Task<User> Login(User user)
{
    return null;
}

public async Task Register(User user)
{
    var test = ""fdsre"";
}";

var pattern = @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\([^)]*\)";

var regex = new Regex(pattern, RegexOptions.Multiline);
var matches = regex.Matches(content);

Console.WriteLine($"Found {matches.Count} matches:");

foreach (Match match in matches)
{
    Console.WriteLine($"Method name: '{match.Groups[2].Value}'");
    Console.WriteLine($"Is async: {!string.IsNullOrEmpty(match.Groups[1].Value)}");
    Console.WriteLine($"Full match: '{match.Value}'");
    Console.WriteLine("---");
}