namespace CodeReviewBot.Shared.Constants;

public static class BotConstants
{
    public const string BotName = "Intelligent C# Code Review Bot";
    public const string BotVersion = "1.0.0";
    public const string DefaultRulesFile = "coding-standards.json";

    // Event Types
    public const string PullRequestCreated = "git.pullrequest.created";
    public const string PullRequestUpdated = "git.pullrequest.updated";

    // Severity Levels
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Info = "Info";

    // File Extensions
    public static readonly string[] SupportedFileExtensions = { ".cs", ".csx" };

    // Limits
    public const int MaxCommentsPerFile = 50;
    public const int MaxConcurrentFiles = 10;
    public const int MaxFileSizeKB = 1024;

    // API Versions
    public const string AzureDevOpsApiVersion = "7.0";
    public const int DefaultTimeoutSeconds = 30;
}
