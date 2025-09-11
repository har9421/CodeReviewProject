# Code Review Project

A custom **code review automation tool** for:

- **C# (.NET Core)** using Roslyn + custom rules
- **React.js** using ESLint

## Features

- Reads coding standards from remote JSON file
- Runs analyzers on C# and JS/TS files
- Posts comments directly on Azure DevOps PR
- Blocks PR on error-level issues

## Usage

1. Set up pipeline with `azure-pipelines.yml`
2. Provide `CODING_STANDARDS_URL` in pipeline variables
3. Open a PR → pipeline runs → comments posted inline
4. Ensure OAuth token is enabled for PR comments
