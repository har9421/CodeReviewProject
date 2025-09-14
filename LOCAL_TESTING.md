# Local Testing Guide for CodeReviewRunner

This guide explains how to test the CodeReviewRunner project locally without needing Azure DevOps.

## Prerequisites

1. **.NET 8.0 SDK** - Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
2. **Node.js** (for React/JS analysis) - Install from [nodejs.org](https://nodejs.org/)
3. **Git** - For cloning and testing

## Quick Start

### 1. Build the Project

```bash
# Navigate to the project directory
cd /Users/code/CodeReviewProject

# Build the solution
dotnet build

# Or build and run tests
dotnet test
```

### 2. Test with Sample Data

The easiest way to test locally is using the test mode with sample files:

```bash
# Run with sample data (no Azure DevOps required)
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "test" \
  "https://raw.githubusercontent.com/your-org/coding-standards/main/rules.json" \
  "test" \
  "https://dev.azure.com/khUniverse" \
  "sso" \
  "test-repo-id"
```

## Testing Modes

### Mode 1: Test with Local Files (Recommended)

Create a test directory with sample code files:

```bash
# Create test directory
mkdir -p test-files

# Create sample C# file
cat > test-files/sample.cs << 'EOF'
using System;

public class TestClass
{
    public void MethodWithLongNameThatExceedsTheMaximumAllowedLength()
    {
        // This method name is too long
        Console.WriteLine("Hello World");
    }

    public void GoodMethod()
    {
        // This method name is fine
        Console.WriteLine("Hello World");
    }
}
EOF

# Create sample React file
cat > test-files/sample.tsx << 'EOF'
import React from 'react';

const ComponentWithLongNameThatExceedsTheMaximumAllowedLength = () => {
    // This component name is too long
    return <div>Hello World</div>;
};

const GoodComponent = () => {
    // This component name is fine
    return <div>Hello World</div>;
};

export default GoodComponent;
EOF
```

### Mode 2: Test with Azure DevOps (Real API)

If you want to test with real Azure DevOps data:

```bash
# Set your Azure DevOps Personal Access Token
export SYSTEM_ACCESSTOKEN="your-pat-token-here"

# Run with real Azure DevOps data
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "/path/to/local/repo" \
  "https://raw.githubusercontent.com/your-org/coding-standards/main/rules.json" \
  "128" \
  "https://dev.azure.com/khUniverse" \
  "sso" \
  "801d272d-36b5-4f23-9674-01aa63f48ce8"
```

## Sample Coding Standards

Create a sample coding standards file for testing:

```json
{
  "rules": [
    {
      "id": "method-name-length",
      "severity": "warning",
      "message": "Method name should not exceed 30 characters",
      "languages": ["csharp"],
      "pattern": "method.*name.*length"
    },
    {
      "id": "component-name-length",
      "severity": "error",
      "message": "Component name should not exceed 25 characters",
      "languages": ["typescript", "javascript"],
      "pattern": "component.*name.*length"
    }
  ]
}
```

## Debugging

### Enable Verbose Logging

Add this to your `Program.cs` for more detailed output:

```csharp
// Add at the beginning of Main method
Console.WriteLine("=== CodeReviewRunner Debug Mode ===");
Console.WriteLine($"Arguments: {string.Join(" ", args)}");
```

### Test Individual Components

You can test individual analyzers:

```bash
# Test only C# analyzer
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "test-files" \
  "https://raw.githubusercontent.com/your-org/coding-standards/main/rules.json" \
  "test" \
  "https://dev.azure.com/khUniverse" \
  "sso" \
  "test-repo-id"
```

## Common Issues and Solutions

### Issue: "SYSTEM_ACCESSTOKEN is not set"

**Solution**: Set the environment variable or use test mode

```bash
export SYSTEM_ACCESSTOKEN="your-token"
# OR use test mode (no token required)
```

### Issue: "Failed to fetch PR changes"

**Solution**: Use test mode or check your Azure DevOps credentials

```bash
# Use test mode instead
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj test ...
```

### Issue: "No changed files detected"

**Solution**: Ensure you have analyzable files (.cs, .js, .ts, .tsx, .jsx) in your test directory

## Testing Checklist

- [ ] Project builds successfully (`dotnet build`)
- [ ] Tests pass (`dotnet test`)
- [ ] Sample C# file is analyzed
- [ ] Sample React file is analyzed
- [ ] Rules are loaded from URL
- [ ] Issues are detected and reported
- [ ] No Azure DevOps errors in test mode

## Next Steps

1. **Create test files** with various code quality issues
2. **Set up sample coding standards** JSON file
3. **Test with different file types** (.cs, .js, .ts, .tsx, .jsx)
4. **Verify rule matching** works correctly
5. **Test error handling** with invalid inputs

## Troubleshooting

If you encounter issues:

1. Check the console output for detailed error messages
2. Verify all dependencies are installed
3. Ensure file paths are correct
4. Check network connectivity for rule fetching
5. Verify coding standards JSON format

For more help, check the main README.md or create an issue in the repository.
