using System.Text.RegularExpressions;
using CodeReviewBot.Shared.Constants;

namespace CodeReviewBot.Shared.Extensions;

public static class StringExtensions
{
    public static bool IsValidCSharpFile(this string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        return BotConstants.SupportedFileExtensions.Any(ext =>
            filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToPascalCase(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }

    public static string SanitizeForComment(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Remove potential markdown that could break comments
        return input.Replace("```", "` ` `")
                   .Replace("**", "*")
                   .Replace("__", "_");
    }

    public static bool ContainsSqlInjectionPattern(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        var patterns = new[]
        {
            @"SELECT\s+.*\s+FROM",
            @"INSERT\s+INTO",
            @"UPDATE\s+.*\s+SET",
            @"DELETE\s+FROM",
            @"DROP\s+TABLE",
            @"UNION\s+SELECT"
        };

        return patterns.Any(pattern =>
            Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
    }
}
