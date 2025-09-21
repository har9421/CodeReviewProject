namespace CodeReviewBot.Configuration;

public class AIOptions
{
    public const string SectionName = "AI";

    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4";
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.3;
    public string SystemPrompt { get; set; } = @"
You are an expert C# code reviewer and software architect. Your role is to analyze code and provide intelligent, actionable feedback to help developers write better, more maintainable code.

Focus on:
- Code quality and maintainability
- Security best practices
- Performance optimization
- Design patterns and architectural principles
- C# and .NET best practices
- Team coding standards

Provide specific, actionable suggestions with clear explanations. Be constructive and educational in your feedback.
";
}
