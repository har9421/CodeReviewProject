#!/bin/bash

# Test script for webhook endpoint
# Usage: ./test-webhook.sh [ngrok-url]

NGROK_URL=${1:-"https://be019c33aeba.ngrok-free.app"}
WEBHOOK_URL="$NGROK_URL/api/webhook"
TEST_URL="$NGROK_URL/api/webhook/test"

echo "🧪 Testing Code Review Bot Webhook"
echo "=================================="
echo "Webhook URL: $WEBHOOK_URL"
echo "Test URL: $TEST_URL"
echo ""

# Test health endpoint
echo "1. Testing health endpoint..."
curl -s "$NGROK_URL/api/webhook/health" | jq '.' 2>/dev/null || echo "Health check failed or jq not installed"
echo ""

# Test webhook with sample payload
echo "2. Testing webhook with sample payload..."
curl -X POST "$WEBHOOK_URL" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "git.pullrequest.created",
    "resource": {
      "pullRequestId": 1,
      "repository": {
        "name": "Fabrikam",
        "project": {
          "name": "Fabrikam"
        },
        "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079"
      }
    },
    "resourceContainers": {
      "project": {
        "id": "be9b3917-87e6-42a4-a549-2bc06a7a878f"
      }
    }
  }' | jq '.' 2>/dev/null || echo "Webhook test failed or jq not installed"
echo ""

# Test with the exact payload you provided
echo "3. Testing with your exact payload..."
curl -X POST "$TEST_URL" \
  -H "Content-Type: application/json" \
  -d '{
    "subscriptionId": "d9615f5f-cb45-4cfd-b239-734ca1a5aad7",
    "notificationId": 14,
    "id": "2ab4e3d3-b7a6-425e-92b1-5a9982c1269e",
    "eventType": "git.pullrequest.created",
    "publisherId": "tfs",
    "message": {
      "text": "Jamal Hartnett created a new pull request",
      "html": "Jamal Hartnett created a new pull request",
      "markdown": "Jamal Hartnett created a new pull request"
    },
    "detailedMessage": {
      "text": "Jamal Hartnett created a new pull request\r\n\r\n- Merge status: Succeeded\r\n- Merge commit: eef717(https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/eef717f69257a6333f221566c1c987dc94cc0d72)\r\n",
      "html": "Jamal Hartnett created a new pull request\r\n<ul>\r\n<li>Merge status: Succeeded</li>\r\n<li>Merge commit: <a href=\"https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/eef717f69257a6333f221566c1c987dc94cc0d72\">eef717</a></li>\r\n</ul>",
      "markdown": "Jamal Hartnett created a new pull request\r\n\r\n+ Merge status: Succeeded\r\n+ Merge commit: [eef717](https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/eef717f69257a6333f221566c1c987dc94cc0d72)\r\n"
    },
    "resource": {
      "repository": {
        "id": "4bc14d40-c903-45e2-872e-0462c7748079",
        "name": "Fabrikam",
        "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079",
        "project": {
          "id": "6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
          "name": "Fabrikam",
          "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/projects/6ce954b1-ce1f-45d1-b94d-e6bf2464ba2c",
          "state": "wellFormed",
          "visibility": "unchanged",
          "lastUpdateTime": "0001-01-01T00:00:00"
        },
        "defaultBranch": "refs/heads/master",
        "remoteUrl": "https://fabrikam.visualstudio.com/DefaultCollection/_git/Fabrikam"
      },
      "pullRequestId": 1,
      "status": "active",
      "createdBy": {
        "displayName": "Jamal Hartnett",
        "url": "https://fabrikam.vssps.visualstudio.com/_apis/Identities/54d125f7-69f7-4191-904f-c5b96b6261c8",
        "id": "54d125f7-69f7-4191-904f-c5b96b6261c8",
        "uniqueName": "fabrikamfiber4@hotmail.com",
        "imageUrl": "https://fabrikam.visualstudio.com/DefaultCollection/_api/_common/identityImage?id=54d125f7-69f7-4191-904f-c5b96b6261c8"
      },
      "creationDate": "2014-06-17T16:55:46.589889Z",
      "title": "my first pull request",
      "description": " - test2\r\n",
      "sourceRefName": "refs/heads/mytopic",
      "targetRefName": "refs/heads/master",
      "mergeStatus": "succeeded",
      "mergeId": "a10bb228-6ba6-4362-abd7-49ea21333dbd",
      "lastMergeSourceCommit": {
        "commitId": "53d54ac915144006c2c9e90d2c7d3880920db49c",
        "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/53d54ac915144006c2c9e90d2c7d3880920db49c"
      },
      "lastMergeTargetCommit": {
        "commitId": "a511f535b1ea495ee0c903badb68fbc83772c882",
        "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/a511f535b1ea495ee0c903badb68fbc83772c882"
      },
      "lastMergeCommit": {
        "commitId": "eef717f69257a6333f221566c1c987dc94cc0d72",
        "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/eef717f69257a6333f221566c1c987dc94cc0d72"
      },
      "reviewers": [
        {
          "reviewerUrl": null,
          "vote": 0,
          "displayName": "[Mobile]\\Mobile Team",
          "url": "https://fabrikam.vssps.visualstudio.com/_apis/Identities/2ea2d095-48f9-4cd6-9966-62f6f574096c",
          "id": "2ea2d095-48f9-4cd6-9966-62f6f574096c",
          "uniqueName": "vstfs:///Classification/TeamProject/f0811a3b-8c8a-4e43-a3bf-9a049b4835bd\\Mobile Team",
          "imageUrl": "https://fabrikam.visualstudio.com/DefaultCollection/_api/_common/identityImage?id=2ea2d095-48f9-4cd6-9966-62f6f574096c",
          "isContainer": true
        }
      ],
      "commits": [
        {
          "commitId": "53d54ac915144006c2c9e90d2c7d3880920db49c",
          "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/commits/53d54ac915144006c2c9e90d2c7d3880920db49c"
        }
      ],
      "url": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/pullRequests/1",
      "_links": {
        "web": {
          "href": "https://fabrikam.visualstudio.com/DefaultCollection/_git/Fabrikam/pullrequest/1#view=discussion"
        },
        "statuses": {
          "href": "https://fabrikam.visualstudio.com/DefaultCollection/_apis/git/repositories/4bc14d40-c903-45e2-872e-0462c7748079/pullRequests/1/statuses"
        }
      }
    },
    "resourceVersion": "1.0",
    "resourceContainers": {
      "collection": {
        "id": "c12d0eb8-e382-443b-9f9c-c52cba5014c2"
      },
      "account": {
        "id": "f844ec47-a9db-4511-8281-8b63f4eaf94e"
      },
      "project": {
        "id": "be9b3917-87e6-42a4-a549-2bc06a7a878f"
      }
    },
    "createdDate": "2025-09-27T18:43:26.2380087Z"
  }' | jq '.' 2>/dev/null || echo "Full payload test failed or jq not installed"
echo ""

echo "✅ Webhook testing completed!"
echo ""
echo "💡 Tips:"
echo "- If you see errors, check the bot logs for detailed information"
echo "- Make sure AZURE_DEVOPS_PAT environment variable is set for full functionality"
echo "- The test endpoint (/api/webhook/test) helps debug payload parsing"
