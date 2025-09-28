# 🗑️ Delete Azure DevOps Service Hooks

## 📋 **Step-by-Step Instructions**

### **1. Navigate to Azure DevOps**

1. Go to: **https://dev.azure.com/khUniverse**
2. Select your project (e.g., **sso**)

### **2. Access Service Hooks Settings**

1. Click on **"Project Settings"** (gear icon in bottom left)
2. In the left sidebar, click **"Service hooks"**
3. You'll see a list of existing webhook subscriptions

### **3. Delete Existing Webhooks**

1. **Find webhooks** with URLs containing:
   - `ngrok-free.app`
   - `ngrok.io`
   - Any previous ngrok URLs
2. **For each webhook**:
   - Click on the webhook name
   - Click **"Delete"** button
   - Confirm deletion

### **4. Common Webhook Names to Delete**

Look for webhooks with names like:

- "Pull Request Created"
- "Pull Request Updated"
- "Code Review Bot"
- Any webhook pointing to ngrok URLs

### **5. Verify Deletion**

- Ensure the webhooks list is empty or contains only non-ngrok webhooks
- Take note of any webhooks you want to keep

## ⚠️ **Important Notes**

- Only delete webhooks that point to ngrok URLs
- Keep any production webhooks you want to maintain
- You can always recreate webhooks later

## ✅ **Next Step**

After deleting old webhooks, run:

```bash
./complete-setup.sh
```

This will give you a fresh ngrok URL and instructions to create new webhooks.
