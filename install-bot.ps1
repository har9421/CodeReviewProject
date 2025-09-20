# Intelligent C# Code Review Bot Installation Script
# This script helps you install the bot as an Azure DevOps extension

param(
    [Parameter(Mandatory=$true)]
    [string]$PublisherId,
    
    [Parameter(Mandatory=$true)]
    [string]$OrganizationUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$PersonalAccessToken,
    
    [Parameter(Mandatory=$false)]
    [string]$BotServiceUrl = "https://your-bot-service.azurewebsites.net"
)

Write-Host "🤖 Installing Intelligent C# Code Review Bot..." -ForegroundColor Green

# Check prerequisites
Write-Host "📋 Checking prerequisites..." -ForegroundColor Yellow

# Check if tfx-cli is installed
try {
    $tfxVersion = tfx --version
    Write-Host "✅ TFX CLI found: $tfxVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ TFX CLI not found. Installing..." -ForegroundColor Red
    npm install -g tfx-cli
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install TFX CLI. Please install Node.js and npm first." -ForegroundColor Red
        exit 1
    }
}

# Check if .NET 8 SDK is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8 SDK not found. Please install it from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

# Build the bot service
Write-Host "🔨 Building bot service..." -ForegroundColor Yellow
Set-Location "src/CodeReviewBot"
dotnet restore
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to build bot service" -ForegroundColor Red
    exit 1
}

# Publish the bot service
Write-Host "📦 Publishing bot service..." -ForegroundColor Yellow
dotnet publish --configuration Release --output "../../dist/bot-service"
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to publish bot service" -ForegroundColor Red
    exit 1
}

Set-Location "../.."

# Update extension manifest with publisher ID
Write-Host "⚙️ Configuring extension manifest..." -ForegroundColor Yellow
$manifest = Get-Content "vss-extension.json" | ConvertFrom-Json
$manifest.publisher = $PublisherId
$manifest | ConvertTo-Json -Depth 10 | Set-Content "vss-extension.json"

# Create extension package
Write-Host "📦 Creating extension package..." -ForegroundColor Yellow
tfx extension create --manifest-globs vss-extension.json --output-path ./dist
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create extension package" -ForegroundColor Red
    exit 1
}

# Upload extension to Azure DevOps
if ($PersonalAccessToken) {
    Write-Host "🚀 Uploading extension to Azure DevOps..." -ForegroundColor Yellow
    tfx extension publish --manifest-globs vss-extension.json --token $PersonalAccessToken
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to upload extension" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "⚠️ Personal Access Token not provided. Extension package created but not uploaded." -ForegroundColor Yellow
    Write-Host "📁 Extension package location: ./dist/codereview-bot-1.0.0.vsix" -ForegroundColor Cyan
    Write-Host "🔗 You can upload it manually at: $OrganizationUrl/_settings/extensions" -ForegroundColor Cyan
}

# Create webhook configuration script
Write-Host "🔗 Creating webhook configuration script..." -ForegroundColor Yellow
$webhookScript = @"
# Webhook Configuration for Code Review Bot
# Run this script after installing the extension

param(
    [Parameter(Mandatory=`$true)]
    [string]`$OrganizationUrl,
    
    [Parameter(Mandatory=`$true)]
    [string]`$ProjectName,
    
    [Parameter(Mandatory=`$true)]
    [string]`$PersonalAccessToken
)

# Bot webhook URL
`$webhookUrl = "$BotServiceUrl/api/webhook"

# Create webhook subscription
`$headers = @{
    "Authorization" = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":`$PersonalAccessToken"))
    "Content-Type" = "application/json"
}

`$webhookPayload = @{
    publisherId = "tfs"
    eventType = "git.pullrequest.created"
    resourceVersion = "1.0"
    consumerId = "webHooks"
    consumerActionId = "httpRequest"
    publisherInputs = @{
        projectId = "`$ProjectName"
    }
    consumerInputs = @{
        url = "`$webhookUrl"
        httpHeaders = @{
            "Content-Type" = "application/json"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    `$response = Invoke-RestMethod -Uri "`$OrganizationUrl/_apis/hooks/subscriptions?api-version=6.0" -Method POST -Headers `$headers -Body `$webhookPayload
    Write-Host "✅ Webhook created successfully with ID: `$(`$response.id)" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create webhook: `$(`$_.Exception.Message)" -ForegroundColor Red
}
"@

$webhookScript | Out-File -FilePath "configure-webhook.ps1" -Encoding UTF8

Write-Host "🎉 Installation completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next steps:" -ForegroundColor Yellow
Write-Host "1. Install the extension in your Azure DevOps organization" -ForegroundColor White
Write-Host "2. Configure the bot by running: .\configure-webhook.ps1" -ForegroundColor White
Write-Host "3. Set up your coding standards JSON file" -ForegroundColor White
Write-Host "4. Configure AI settings if desired" -ForegroundColor White
Write-Host ""
Write-Host "🔗 Bot service URL: $BotServiceUrl" -ForegroundColor Cyan
Write-Host "📚 Documentation: See README-Bot.md for detailed setup instructions" -ForegroundColor Cyan
