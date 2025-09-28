#!/bin/bash

# Check if ngrok URL is provided
if [ -z "$1" ]; then
    echo "Usage: $0 <ngrok_url>"
    echo "Example: $0 https://your-ngrok-url.ngrok-free.app"
    exit 1
fi

NGROK_URL="$1"
WEBHOOK_ENDPOINT="${NGROK_URL}/api/webhook"

echo "🧪 Testing with Real Azure DevOps Webhook Event"
echo "Webhook endpoint: ${WEBHOOK_ENDPOINT}"
echo ""

# Test health endpoint first
echo "--- Testing Health Endpoint ---"
curl -s "${WEBHOOK_ENDPOINT}/health"
echo ""
echo ""

# Test with the real webhook event from your PR #148
echo "--- Testing with Real PR #148 Webhook Event ---"
REAL_WEBHOOK_PAYLOAD='{
    "id": "a9591ad1-8284-4e65-9b5d-b8cc4b726afc",
    "eventType": "git.pullrequest.created",
    "publisherId": "tfs",
    "message": {
        "text": "Harshad Karemore created pull request 148 (dfs) in authservice\r\nhttps://dev.azure.com/khUniverse/sso/_git/authservice/",
        "html": "Harshad Karemore created <a href=\"https://dev.azure.com/khUniverse/sso/_git/authservice/pullrequest/148\">pull request 148</a> (dfs) in <a href=\"https://dev.azure.com/khUniverse/sso/_git/authservice/\">authservice</a>",
        "markdown": "Harshad Karemore created [pull request 148](https://dev.azure.com/khUniverse/sso/_git/authservice/pullrequest/148) (dfs) in [authservice](https://dev.azure.com/khUniverse/sso/_git/authservice/)"
    },
    "detailedMessage": {
        "text": "Harshad Karemore created pull request 148 (dfs) in authservice\r\nhttps://dev.azure.com/khUniverse/sso/_git/authservice/",
        "html": "Harshad Karemore created <a href=\"https://dev.azure.com/khUniverse/sso/_git/authservice/pullrequest/148\">pull request 148</a> (dfs) in <a href=\"https://dev.azure.com/khUniverse/sso/_git/authservice/\">authservice</a>",
        "markdown": "Harshad Karemore created [pull request 148](https://dev.azure.com/khUniverse/sso/_git/authservice/pullrequest/148) (dfs) in [authservice](https://dev.azure.com/khUniverse/sso/_git/authservice/)"
    },
    "resource": {
        "repository": {
            "id": "801d272d-36b5-4f23-9674-01aa63f48ce8",
            "name": "authservice",
            "url": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8",
            "project": {
                "id": "5b8147cc-d3f4-4d68-9d0b-69090c123bcd",
                "name": "sso",
                "url": "https://dev.azure.com/khUniverse/_apis/projects/5b8147cc-d3f4-4d68-9d0b-69090c123bcd",
                "state": "wellFormed",
                "revision": 24,
                "visibility": "private",
                "lastUpdateTime": "2025-02-15T17:52:21.073Z"
            },
            "size": 93754,
            "remoteUrl": "https://khUniverse@dev.azure.com/khUniverse/sso/_git/authservice",
            "sshUrl": "git@ssh.dev.azure.com:v3/khUniverse/sso/authservice",
            "webUrl": "https://dev.azure.com/khUniverse/sso/_git/authservice",
            "isDisabled": false,
            "isInMaintenance": false
        },
        "pullRequestId": 148,
        "codeReviewId": 148,
        "status": "active",
        "createdBy": {
            "displayName": "Harshad Karemore",
            "url": "https://spsprodcin1.vssps.visualstudio.com/Af77ebe80-b988-4458-8598-df39236a9d76/_apis/Identities/7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
            "_links": {
                "avatar": {
                    "href": "https://dev.azure.com/khUniverse/_apis/GraphProfile/MemberAvatars/msa.N2JmMWUyMDAtOGVkNy03MjEzLWJkZWYtOWI3MGVlMmQ3MzFl"
                }
            },
            "id": "7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
            "uniqueName": "harshad.karemore7@gmail.com",
            "imageUrl": "https://dev.azure.com/khUniverse/_api/_common/identityImage?id=7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
            "descriptor": "msa.N2JmMWUyMDAtOGVkNy03MjEzLWJkZWYtOWI3MGVlMmQ3MzFl"
        },
        "creationDate": "2025-09-28T15:21:18.3897123Z",
        "title": "dfs",
        "sourceRefName": "refs/heads/feature/harshad/connectionstringchanges",
        "targetRefName": "refs/heads/develop",
        "mergeStatus": "succeeded",
        "isDraft": false,
        "mergeId": "91df896b-8174-4438-bd19-a41d878b77aa",
        "lastMergeSourceCommit": {
            "commitId": "7e0c30910391302f934883fb4850bcee2b072c03",
            "url": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/commits/7e0c30910391302f934883fb4850bcee2b072c03"
        },
        "lastMergeTargetCommit": {
            "commitId": "600efebaf8098e7571d33e23813ed91a78cf65bb",
            "url": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/commits/600efebaf8098e7571d33e23813ed91a78cf65bb"
        },
        "lastMergeCommit": {
            "commitId": "622127b672b02b09cb046c471f30d9cc93d51ea6",
            "author": {
                "name": "Harshad Karemore",
                "email": "harshad.karemore7@gmail.com",
                "date": "2025-09-28T15:21:18Z"
            },
            "committer": {
                "name": "Harshad Karemore",
                "email": "harshad.karemore7@gmail.com",
                "date": "2025-09-28T15:21:18Z"
            },
            "comment": "Merge pull request 148 from feature/harshad/connectionstringchanges into develop",
            "url": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/commits/622127b672b02b09cb046c471f30d9cc93d51ea6"
        },
        "reviewers": [
            {
                "reviewerUrl": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/148/reviewers/7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
                "vote": 0,
                "hasDeclined": false,
                "isRequired": true,
                "isFlagged": false,
                "displayName": "Harshad Karemore",
                "url": "https://spsprodcin1.vssps.visualstudio.com/Af77ebe80-b988-4458-8598-df39236a9d76/_apis/Identities/7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
                "_links": {
                    "avatar": {
                        "href": "https://dev.azure.com/khUniverse/_apis/GraphProfile/MemberAvatars/msa.N2JmMWUyMDAtOGVkNy03MjEzLWJkZWYtOWI3MGVlMmQ3MzFl"
                    }
                },
                "id": "7bf1e200-8ed7-6213-bdef-9b70ee2d731e",
                "uniqueName": "harshad.karemore7@gmail.com",
                "imageUrl": "https://dev.azure.com/khUniverse/_api/_common/identityImage?id=7bf1e200-8ed7-6213-bdef-9b70ee2d731e"
            }
        ],
        "url": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/148",
        "_links": {
            "web": {
                "href": "https://dev.azure.com/khUniverse/sso/_git/authservice/pullrequest/148"
            },
            "statuses": {
                "href": "https://dev.azure.com/khUniverse/5b8147cc-d3f4-4d68-9d0b-69090c123bcd/_apis/git/repositories/801d272d-36b5-4f23-9674-01aa63f48ce8/pullRequests/148/statuses"
            }
        },
        "supportsIterations": true,
        "artifactId": "vstfs:///Git/PullRequestId/5b8147cc-d3f4-4d68-9d0b-69090c123bcd%2f801d272d-36b5-4f23-9674-01aa63f48ce8%2f148"
    },
    "resourceVersion": "1.0",
    "resourceContainers": {
        "collection": {
            "id": "3c359f0d-4104-4885-977b-1b3743c7559d",
            "baseUrl": "https://dev.azure.com/khUniverse/"
        },
        "account": {
            "id": "f77ebe80-b988-4458-8598-df39236a9d76",
            "baseUrl": "https://dev.azure.com/khUniverse/"
        },
        "project": {
            "id": "5b8147cc-d3f4-4d68-9d0b-69090c123bcd",
            "baseUrl": "https://dev.azure.com/khUniverse/"
        }
    },
    "createdDate": "2025-09-28T15:21:24.978Z"
}'

curl -X POST -H "Content-Type: application/json" -d "$REAL_WEBHOOK_PAYLOAD" "${WEBHOOK_ENDPOINT}"
echo ""
echo ""

echo "🎉 Test completed!"
echo ""
echo "Expected behavior:"
echo "✅ Bot should receive the webhook"
echo "✅ Bot should extract PR #148 details"
echo "✅ Bot should analyze C# files (if PAT is configured)"
echo "✅ Bot should post comments on the PR (if PAT is configured)"
echo ""
echo "If you see 'PAT not configured' message, set your PAT:"
echo "export AZURE_DEVOPS_PAT=\"your-pat-token-here\""
