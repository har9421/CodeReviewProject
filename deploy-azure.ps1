# Azure App Service Deployment Script for Code Review Bot
# This script creates Azure resources and deploys the bot

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppServiceName,
    
    [Parameter(Mandatory=$true)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$false)]
    [string]$AppServicePlanName = "$AppServiceName-plan",
    
    [Parameter(Mandatory=$false)]
    [string]$WebhookSecret = ([System.Guid]::NewGuid().ToString() + [System.Guid]::NewGuid().ToString())
)

Write-Host "🚀 Deploying Code Review Bot to Azure App Service..." -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Cyan
Write-Host "App Service: $AppServiceName" -ForegroundColor Cyan
Write-Host "Location: $Location" -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✅ Azure CLI found (version: $($azVersion.'azure-cli'))" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI not found. Please install it first:" -ForegroundColor Red
    Write-Host "   https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}

# Check if logged in to Azure
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "❌ Not logged in to Azure. Please run: az login" -ForegroundColor Red
    exit 1
}

Write-Host "`n📦 Step 1: Creating Resource Group..." -ForegroundColor Yellow
try {
    az group create --name $ResourceGroupName --location $Location
    Write-Host "✅ Resource group created" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create resource group: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n📋 Step 2: Creating App Service Plan..." -ForegroundColor Yellow
try {
    az appservice plan create --name $AppServicePlanName --resource-group $ResourceGroupName --location $Location --sku B1 --is-linux
    Write-Host "✅ App Service plan created" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create App Service plan: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n🌐 Step 3: Creating App Service..." -ForegroundColor Yellow
try {
    az webapp create --name $AppServiceName --resource-group $ResourceGroupName --plan $AppServicePlanName --runtime "DOTNET|8.0"
    Write-Host "✅ App Service created" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create App Service: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n⚙️ Step 4: Configuring App Settings..." -ForegroundColor Yellow
try {
    az webapp config appsettings set --name $AppServiceName --resource-group $ResourceGroupName --settings `
        "Bot__Webhook__Secret=$WebhookSecret" `
        "Bot__DefaultRulesUrl=coding-standards.json" `
        "Bot__Name=Intelligent C# Code Review Bot" `
        "ASPNETCORE_ENVIRONMENT=Production"
    Write-Host "✅ App settings configured" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to configure app settings: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n📤 Step 5: Deploying Application..." -ForegroundColor Yellow
try {
    # Create deployment package
    Write-Host "Creating deployment package..." -ForegroundColor Gray
    Push-Location "dist"
    Compress-Archive -Path * -DestinationPath "../codereview-bot.zip" -Force
    Pop-Location
    
    # Deploy to Azure
    Write-Host "Deploying to Azure..." -ForegroundColor Gray
    az webapp deployment source config-zip --name $AppServiceName --resource-group $ResourceGroupName --src "codereview-bot.zip"
    Write-Host "✅ Application deployed" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to deploy application: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Get the App Service URL
$appServiceUrl = "https://$AppServiceName.azurewebsites.net"

Write-Host "`n🎉 Deployment Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "App Service URL: $appServiceUrl" -ForegroundColor White
Write-Host "Webhook Endpoint: $appServiceUrl/api/webhook" -ForegroundColor White
Write-Host "Health Check: $appServiceUrl/api/webhook/health" -ForegroundColor White
Write-Host "Webhook Secret: $WebhookSecret" -ForegroundColor Yellow
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Test the health endpoint:" -ForegroundColor White
Write-Host "   curl $appServiceUrl/api/webhook/health" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Configure webhook in Azure DevOps:" -ForegroundColor White
Write-Host "   .\configure-webhook.ps1 -OrganizationUrl 'https://dev.azure.com/yourorg' -ProjectName 'YourProject' -PersonalAccessToken 'your-pat' -BotServiceUrl '$appServiceUrl'" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Test with a pull request in your Azure DevOps project" -ForegroundColor White
Write-Host ""
Write-Host "🔐 Security Note: Save your webhook secret securely!" -ForegroundColor Yellow
