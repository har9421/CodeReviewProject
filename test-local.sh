#!/bin/bash

# Local Testing Script for CodeReviewRunner
# This script helps you test the CodeReviewRunner locally without Azure DevOps

echo "=== CodeReviewRunner Local Testing ==="
echo

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET is not installed. Please install .NET 8.0 SDK first."
    echo "   Download from: https://dotnet.microsoft.com/download"
    exit 1
fi

echo "✅ .NET is installed: $(dotnet --version)"

# Build the project
echo
echo "🔨 Building the project..."
if ! dotnet build; then
    echo "❌ Build failed. Please fix the build errors first."
    exit 1
fi

echo "✅ Build successful"

# Run tests
echo
echo "🧪 Running tests..."
if ! dotnet test; then
    echo "❌ Tests failed. Please fix the test errors first."
    exit 1
fi

echo "✅ Tests passed"

# Test with sample data
echo
echo "🚀 Running CodeReviewRunner in test mode..."

# Create a temporary coding standards URL (you can replace this with your own)
CODING_STANDARDS_URL="file://$(pwd)/sample-coding-standards.json"

echo "Using coding standards from: $CODING_STANDARDS_URL"
echo

# Run the application in test mode
dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "test" \
  "$CODING_STANDARDS_URL" \
  "test" \
  "https://dev.azure.com/khUniverse" \
  "sso" \
  "test-repo-id"

echo
echo "=== Test completed ==="
echo
echo "To test with your own coding standards:"
echo "1. Update the CODING_STANDARDS_URL variable in this script"
echo "2. Or run manually:"
echo "   dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj test <your-rules-url> test <org-url> <project> <repo-id>"
