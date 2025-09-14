#!/bin/bash

# Test script for the Azure DevOps API fix
echo "Testing Azure DevOps API fix..."
echo "================================="

# Set your PAT token here
export SYSTEM_ACCESSTOKEN="${SYSTEM_ACCESSTOKEN:-}"

if [ -z "$SYSTEM_ACCESSTOKEN" ]; then
    echo "❌ SYSTEM_ACCESSTOKEN is not set"
    echo "Please set your Azure DevOps PAT token:"
    echo "export SYSTEM_ACCESSTOKEN='your-pat-token'"
    exit 1
fi

echo "✅ PAT token is set"
echo ""

# Build the project
echo "Building project..."
dotnet build --configuration Release --verbosity quiet

if [ $? -ne 0 ]; then
    echo "❌ Build failed"
    exit 1
fi

echo "✅ Build successful"
echo ""

# Test with the problematic PR
echo "Testing with PR #128..."
echo "Organization: https://dev.azure.com/khUniverse"
echo "Project: sso"
echo "Repository ID: 801d272d-36b5-4f23-9674-01aa63f48ce8"
echo "Pull Request: 128"
echo ""

dotnet run --project src/CodeReviewRunner/CodeReviewRunner.csproj \
  "/tmp/test-repo" \
  "https://raw.githubusercontent.com/microsoft/vscode/main/package.json" \
  "128" \
  "https://dev.azure.com/khUniverse" \
  "sso" \
  "801d272d-36b5-4f23-9674-01aa63f48ce8"

echo ""
echo "Test completed!"
