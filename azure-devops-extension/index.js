const tl = require('azure-pipelines-task-lib/task');
const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

async function run() {
    try {
        console.log('🤖 Starting Intelligent C# Code Review Bot...');

        // Get task inputs
        const rulesUrl = tl.getInput('rulesUrl', true);
        const aiEnabled = tl.getBoolInput('aiEnabled', false);
        const aiApiKey = tl.getInput('aiApiKey', false);
        const aiModel = tl.getInput('aiModel', false);
        const learningEnabled = tl.getBoolInput('learningEnabled', false);
        const maxCommentsPerFile = parseInt(tl.getInput('maxCommentsPerFile', false) || '50');
        const enableSummary = tl.getBoolInput('enableSummary', false);
        const severityThreshold = tl.getInput('severityThreshold', false);

        // Get environment variables
        const buildSourcesDirectory = tl.getVariable('Build.SourcesDirectory');
        const systemAccessToken = tl.getVariable('System.AccessToken');
        const pullRequestId = tl.getVariable('System.PullRequest.PullRequestId');
        const collectionUri = tl.getVariable('System.CollectionUri');
        const teamProject = tl.getVariable('System.TeamProject');
        const repositoryId = tl.getVariable('Build.Repository.ID');

        // Validate required inputs
        if (!buildSourcesDirectory) {
            tl.setResult(tl.TaskResult.Failed, 'Build.SourcesDirectory variable is not available');
            return;
        }

        if (!systemAccessToken) {
            tl.setResult(tl.TaskResult.Failed, 'System.AccessToken is not available. Please enable "Allow scripts to access OAuth token" in pipeline settings.');
            return;
        }

        if (!pullRequestId) {
            console.log('⚠️ Not running in a pull request context. Exiting gracefully.');
            tl.setResult(tl.TaskResult.Succeeded, 'Not a pull request context');
            return;
        }

        // Set up environment variables for the bot
        process.env.SYSTEM_ACCESSTOKEN = systemAccessToken;
        if (aiEnabled && aiApiKey) {
            process.env.AI_API_KEY = aiApiKey;
        }
        process.env.AI_MODEL = aiModel || 'gpt-4';
        process.env.LEARNING_ENABLED = learningEnabled.toString();
        process.env.MAX_COMMENTS_PER_FILE = maxCommentsPerFile.toString();
        process.env.ENABLE_SUMMARY = enableSummary.toString();
        process.env.SEVERITY_THRESHOLD = severityThreshold || 'warning';

        // Build and run the C# bot
        console.log('🔨 Building Code Review Bot...');
        
        const projectPath = path.join(__dirname, '..', 'src', 'CodeReviewRunner');
        
        try {
            // Restore packages
            execSync('dotnet restore', { 
                cwd: projectPath, 
                stdio: 'inherit',
                env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
            });

            // Build the project
            execSync('dotnet build --configuration Release --no-restore', { 
                cwd: projectPath, 
                stdio: 'inherit',
                env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
            });

            // Run the bot
            console.log('🚀 Running Code Review Bot...');
            const command = `dotnet run --configuration Release --no-build -- "${buildSourcesDirectory}" "${rulesUrl}" "${pullRequestId}" "${collectionUri}" "${teamProject}" "${repositoryId}"`;
            
            execSync(command, { 
                cwd: projectPath, 
                stdio: 'inherit',
                env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1' }
            });

            console.log('✅ Code Review Bot completed successfully!');
            tl.setResult(tl.TaskResult.Succeeded, 'Code review completed successfully');

        } catch (buildError) {
            console.error('❌ Error building or running the bot:', buildError.message);
            tl.setResult(tl.TaskResult.Failed, `Bot execution failed: ${buildError.message}`);
        }

    } catch (error) {
        console.error('❌ Unexpected error:', error.message);
        tl.setResult(tl.TaskResult.Failed, `Unexpected error: ${error.message}`);
    }
}

run();
