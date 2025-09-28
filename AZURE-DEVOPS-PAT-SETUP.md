# Azure DevOps Personal Access Token (PAT) Setup Guide

## 🔑 **Step-by-Step PAT Creation**

### **Step 1: Navigate to Azure DevOps**

1. Go to your Azure DevOps organization: **https://dev.azure.com/khUniverse**
2. Click on your **profile picture** in the top-right corner
3. Select **"Personal access tokens"** from the dropdown menu

   _Alternative path: https://dev.azure.com/khUniverse/_usersSettings/tokens_

### **Step 2: Create New Token**

1. Click the **"+ New Token"** button
2. Fill in the token details:

   **Token Name**: `Code Review Bot`

   **Expiration**: Choose appropriate duration

   - **30 days** (for testing)
   - **60 days** (for development)
   - **90 days** (for production)

### **Step 3: Select Scopes (Permissions)**

The bot needs specific permissions to work. Select these scopes:

#### **Required Scopes:**

✅ **Code (read & write)** - To read pull request files and post comments  
✅ **Pull Requests (read & write)** - To fetch PR details and add comments

#### **Optional Scopes (if needed):**

✅ **Build (read)** - If you want to check build status  
✅ **Work Items (read)** - If you want to link work items

### **Step 4: Select Organization**

- Make sure **"khUniverse"** is selected
- This gives the token access to your organization

### **Step 5: Create and Copy Token**

1. Click **"Create"** button
2. **⚠️ IMPORTANT**: Copy the token immediately - you won't see it again!
3. Store it securely (password manager, etc.)

The token will look like: `your-pat-token-here`

## 🚀 **Using the PAT with Your Bot**

### **Method 1: Using the Startup Script (Recommended)**

```bash
# Replace with your actual PAT
./start-bot-with-pat.sh "your-pat-token-here"
```

### **Method 2: Manual Environment Variable**

```bash
# Set the PAT
export AZURE_DEVOPS_PAT="your-pat-token-here"

# Set ngrok flag
export NGROK_URL="https://ngrok.io"

# Start the bot
cd src/CodeReviewBot.Presentation
dotnet run
```

### **Method 3: Create a Permanent Script**

Create a file called `my-bot-start.sh`:

```bash
#!/bin/bash
export AZURE_DEVOPS_PAT="your-actual-pat-here"
export NGROK_URL="https://ngrok.io"
cd src/CodeReviewBot.Presentation
dotnet run
```

Make it executable and run:

```bash
chmod +x my-bot-start.sh
./my-bot-start.sh
```

## ✅ **Verify PAT is Working**

### **Test 1: Check Environment Variable**

```bash
echo $AZURE_DEVOPS_PAT
```

Should show your token (first 10 characters)

### **Test 2: Test Bot Health**

```bash
# Start your bot first, then test
curl -X GET "http://localhost:5002/api/webhook/health"
```

### **Test 3: Test with Webhook**

```bash
# Use your ngrok URL
curl -X POST "https://your-ngrok-url.ngrok-free.app/api/webhook/test" \
  -H "Content-Type: application/json" \
  -d '{"eventType": "git.pullrequest.created"}'
```

## 🔒 **Security Best Practices**

### **Token Security:**

- ✅ Store PAT in password manager
- ✅ Use different PATs for dev/staging/prod
- ✅ Set appropriate expiration dates
- ✅ Rotate tokens regularly

### **Environment Security:**

- ✅ Never commit PAT to git
- ✅ Use environment variables
- ✅ Use `.env` files for local development
- ✅ Use Azure Key Vault for production

## 🛠️ **Troubleshooting**

### **Common Issues:**

#### **"PAT not configured" Error:**

```bash
# Check if PAT is set
echo $AZURE_DEVOPS_PAT

# If empty, set it:
export AZURE_DEVOPS_PAT="your-token-here"
```

#### **"Forbidden" or "Unauthorized" Errors:**

- ✅ Check if PAT has correct scopes
- ✅ Verify PAT hasn't expired
- ✅ Ensure PAT has access to your organization

#### **"Invalid token" Error:**

- ✅ Copy the entire token (no extra spaces)
- ✅ Make sure token is from correct organization
- ✅ Check if token was created with required scopes

## 📋 **Required Permissions Summary**

Your PAT needs these permissions to work with the Code Review Bot:

| Scope             | Permission   | Why Needed                     |
| ----------------- | ------------ | ------------------------------ |
| **Code**          | Read & Write | Read PR files, post comments   |
| **Pull Requests** | Read & Write | Fetch PR details, add comments |

## 🎯 **Quick Setup Checklist**

- [ ] Navigate to Azure DevOps Personal Access Tokens
- [ ] Click "New Token"
- [ ] Name: "Code Review Bot"
- [ ] Select "Code (read & write)" scope
- [ ] Select "Pull Requests (read & write)" scope
- [ ] Set appropriate expiration
- [ ] Click "Create"
- [ ] **Copy the token immediately**
- [ ] Set `AZURE_DEVOPS_PAT` environment variable
- [ ] Start your bot
- [ ] Test with a webhook

## 🚀 **You're Ready!**

Once your PAT is configured, your bot will:

- ✅ Authenticate with Azure DevOps
- ✅ Fetch pull request details
- ✅ Download and analyze C# files
- ✅ Post intelligent comments with findings
- ✅ Work with your webhook events

**Next Step**: Create a test pull request to see your bot in action!
