# Azure DevOps Bot Integration from GitHub

## Quick Setup

### 1. Create GitHub Service Connection in Azure DevOps

- Go to Project Settings → Service connections → New service connection → GitHub
- Choose "GitHub" → Authorize with OAuth → Select your GitHub account
- Name it "GitHub" (or update the YAML endpoint name)
- Grant access to `har9421/CodeReviewProject` repository

### 2. Add Pipeline to Target Repo

- Copy `azure-pipelines-github-bot.yml` to your target repo's root
- Rename it to `azure-pipelines.yml`
- Create pipeline: Pipelines → New pipeline → Azure Repos Git → Existing YAML file → select the YAML

### 3. Enable Permissions

- **OAuth Token**: Pipeline Settings → "Allow scripts to access the OAuth token" = ON
- **PR Comments**: Project Settings → Repositories → {repo} → Security → "Project Collection Build Service" → "Contribute to pull requests" = Allow

### 4. Configure Rules (Optional)

- **Option A**: Set pipeline variable `CODING_STANDARDS_URL` to your JSON rules URL
- **Option B**: No variable needed; uses the bot's sample rules file

## What It Does

- Fetches the latest bot code from GitHub
- Builds and tests the bot
- Analyzes your repo's C# and React/JS files
- Posts inline PR comments and summary
- Fails build on "error" issues

## Manual Testing

```bash
# Clone the bot repo locally
git clone https://github.com/har9421/CodeReviewProject.git
cd CodeReviewProject

# Run against your target repo
dotnet run --project ./src/CodeReviewRunner \
  /path/to/your/target/repo \
  ./coding-standards.sample.json \
  <prId> <orgUrl> <project> <repoId>
```

## Troubleshooting

- **No comments**: Check OAuth token and PR permissions
- **Build fails**: Ensure GitHub service connection has access to the bot repo
- **ESLint errors**: Target repo may need ESLint plugins; bot pins `eslint@9`
