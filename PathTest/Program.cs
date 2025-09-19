using System;
using System.Collections.Generic;

// Test the file path normalization fix
// Simulate the issue paths from the logs
var issuePaths = new[]
{
    "/Services/SSO.API/Program.cs",
    "/Services/SSO.Infrastructure.Data/Repositories/UserRepository.cs"
};

// Simulate the allowed paths (from changed files)
var allowedPaths = new[]
{
    "Services/SSO.API/Program.cs",
    "Services/SSO.Infrastructure.Data/Repositories/UserRepository.cs"
};

// Create allowed set (same logic as in the code)
var allowedSet = new HashSet<string>(allowedPaths, StringComparer.OrdinalIgnoreCase);

Console.WriteLine("Testing file path normalization fix:");
Console.WriteLine();

foreach (var issuePath in issuePaths)
{
    // New logic (fixed)
    var normalized = issuePath.TrimStart('/').Replace('\\', '/');

    // Check if it would match
    var wouldMatch = allowedSet.Contains(normalized);

    Console.WriteLine($"Issue Path: {issuePath}");
    Console.WriteLine($"Normalized: {normalized}");
    Console.WriteLine($"Would Match: {wouldMatch}");
    Console.WriteLine();
}

Console.WriteLine("Allowed paths:");
foreach (var path in allowedPaths)
{
    Console.WriteLine($"  {path}");
}
