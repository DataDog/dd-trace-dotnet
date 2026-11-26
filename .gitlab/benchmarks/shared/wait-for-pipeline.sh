#!/usr/bin/env bash

set -ev

source "$(dirname "${BASH_SOURCE[0]}")/retry.sh"

organization="datadoghq"
project="dd-trace-dotnet"
personalAccessToken="1"
apiVersion="7.0"
stageNames=("package_linux" "package_arm64" "build_windows_tracer" "generate_variables" "merge_commit_id")

if [ "$USE_MASTER_BUILD" = "1" ]; then
    branchName="master"
    echo "Running with USE_MASTER_BUILD=1, build artifacts will be downloaded from default branch $branchName in order to speed up pipeline execution"
elif echo "$CI_REPOSITORY_URL" | grep -q "benchmark"; then
    branchName="master"
    echo "Running from Benchmarking repo, setting Branch to default branch $branchName"
else
    branchName="$CI_COMMIT_REF_NAME"
    echo "Running from dd-trace-dotnet repo, setting Branch to current branch $branchName"
fi

echo "Branch Name set to: $branchName"

if [ -n "$TEST_BRANCH" ]; then
    branchName="master"
    echo "Branch Name set to: $branchName after override!"
fi

if ! response=$(curl_with_retry - -u ":$personalAccessToken" "https://dev.azure.com/$organization/$project/_apis/build/builds?queryOrder=startTimeDescending&api-version=7.1-preview.5"); then
    echo "Failed to get builds from Azure DevOps" >&2
    exit 1
fi

# Only keep runs from 'consolidated-pipeline'
branchBuildId=$(echo $response | jq -r --arg branchName "refs/heads/$branchName" '.value[] | select(.definition.name == "consolidated-pipeline" and .sourceBranch == $branchName) | .id' | head -n 1)

prBuildId=$(echo $response | jq -r --arg branchName "$branchName" '.value[] | select(.definition.name == "consolidated-pipeline" and .triggerInfo["pr.sourceBranch"] == $branchName) | .id' | head -n 1)

# Determine the latest build between branch and PR
if [[ -n "$branchBuildId" && -n "$prBuildId" ]]; then
    # Compare timestamps of both build IDs
    branchTime=$(echo $response | jq -r --arg buildId "$branchBuildId" '.value[] | select(.id == ($buildId | tonumber)) | .startTime')
    prTime=$(echo $response | jq -r --arg buildId "$prBuildId" '.value[] | select(.id == ($buildId | tonumber)) | .startTime')
    
    if [[ "$branchTime" > "$prTime" ]]; then
        export buildId="$branchBuildId"
    else
        export buildId="$prBuildId"
    fi
elif [[ -n "$branchBuildId" ]]; then
    export buildId="$branchBuildId"
elif [[ -n "$prBuildId" ]]; then
    export buildId="$prBuildId"
else
    export buildId=""
fi

echo "Building for the following build https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$latestBuildId&view=results"

if [ -z "$buildId" ]; then
    echo "This branch does not have a build related to it"
    exit 1
fi

timelineUrl="https://dev.azure.com/$organization/$project/_apis/build/builds/$buildId/timeline?api-version=$apiVersion"


for stageName in "${stageNames[@]}"; do
    while true; do
        if ! response=$(curl_with_retry - -u ":$personalAccessToken" "$timelineUrl"); then
            echo "Failed to get timeline for build $buildId from Azure DevOps" >&2
            exit 1
        fi
        echo "Got Timeline"
        status=$(echo "$response" | jq -r --arg STAGE "$stageName" '.records[] | select(.name == $STAGE) | .state')
        result=$(echo "$response" | jq -r --arg STAGE "$stageName" '.records[] | select(.name == $STAGE) | .result')
        echo "Stage '$stageName' status: $status"
        if [ "$status" == "completed" ]; then
            echo "Stage '$stageName' has completed with the following status $status."
            if [ "$result" == "succeeded" ]; then
                echo "Moving on to next stage"
                break
            else
                echo "Error: Stage '$stageName' did not succeed. Listing failing jobs..."
                echo "$response" | jq -r --arg STAGE "$stageName" '.records[] | select(.name == $STAGE and .result != "succeeded") | {name: .name, result: .result}'
                echo "For more details, check for the following build https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId&view=results"
                exit 1
            fi
        else
            echo "Stage '$stageName' is not completed yet. Waiting 60 seconds..."
            echo "Check out where the pipeline is at: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId&view=results"

            sleep 60
        fi
    done
done

echo "export buildId=$buildId" > build-id.txt
echo "export buildId=$buildId" > ../build-id.txt
