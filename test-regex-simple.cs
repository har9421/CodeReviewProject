using System;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        var content = "public async Task<User> Login(User user)";

        var pattern = @"^\s*(?:public|private|protected|internal)?\s*(?:virtual\s+|override\s+|abstract\s+|new\s+|static\s+)*(async\s+)?[\w<>\[\],\s]+\s+([A-Za-z]\w*)\s*\([^)]*\)";

        var regex = new Regex(pattern, RegexOptions.Multiline);
        var match = regex.Match(content);

        if (match.Success)
        {
            Console.WriteLine($"Match found!");
            Console.WriteLine($"Method name: '{match.Groups[2].Value}'");
            Console.WriteLine($"Is async: {!string.IsNullOrEmpty(match.Groups[1].Value)}");
        }
        else
        {
            Console.WriteLine("No match found");
        }
    }
}
