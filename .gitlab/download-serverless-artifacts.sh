#!/bin/bash
#
# This scripts downloads the necessary binaries to be used for the AWS Lambda Layer.
# This artifacts include: Tracer and ClrProfiler
#

set -eo pipefail

# Create a directory to store the files
target_dir=artifacts
mkdir -p $target_dir

if [ -n "$CI_COMMIT_TAG" ] && [ -n "$CI_COMMIT_SHA" ]; then
  echo "Downloading artifacts from Azure"
  curl --location --fail \
    --output $target_dir/serverless-artifacts.zip \
    "https://apmdotnetci.blob.core.windows.net/apm-dotnet-ci-artifacts-master/${CI_COMMIT_SHA}/serverless-artifacts.zip"

  # Extract top level artifact
  unzip $target_dir/serverless-artifacts.zip -d $target_dir/
  rm -f $target_dir/serverless-artifacts.zip

  ls -l $target_dir
  exit 0
fi

branchName="refs/heads/$CI_COMMIT_BRANCH"
artifactName="serverless-artifacts"

echo "Looking for azure devops PR builds for branch '$branchName' for commit '$CI_COMMIT_SHA' to start"

# We should _definitely_ have the build by now, so if not, there probably won't be one
# Check for PR builds first (as more likely to be "full" builds)
allBuildsForPrUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=100&queryOrder=queueTimeDescending&reasonFilter=pullRequest"
buildId=$(curl -sS $allBuildsForPrUrl | jq --arg version $CI_COMMIT_SHA --arg branch $CI_COMMIT_BRANCH '.value[] | select(.triggerInfo["pr.sourceBranch"] == $branch and .triggerInfo["pr.sourceSha"] == $version)  | .id' | head -n 1)

if [ -z "${buildId}" ]; then
  echo "No PR builds found for commit '$CI_COMMIT_SHA' on branch '$branchName'. Checking for standalone builds..."  
  allBuildsForBranchUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=10&queryOrder=queueTimeDescending&branchName=$branchName&reasonFilter=manual,individualCI"
  buildId=$(curl -sS $allBuildsForBranchUrl | jq --arg version $CI_COMMIT_SHA '.value[] | select(.sourceVersion == $version and .reason != "schedule")  | .id' | head -n 1)
fi

if [ -z "${buildId}" ]; then
  echo "No build found for commit '$CI_COMMIT_SHA' on branch '$branchName' (including PRs)"
  exit 1
fi

echo "Found build with id '$buildId' for commit '$CI_COMMIT_SHA' on branch '$branchName'"

# Now try to download the artifacts from the build
artifactsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$buildId/artifacts?api-version=7.1&artifactName=$artifactName"

# Keep trying to get the artifact for 40 minutes
TIMEOUT=2400
STARTED=0
until (( STARTED == TIMEOUT )) || [ ! -z "${downloadUrl}" ] ; do
    echo "Checking for artifacts at: ${artifactsUrl}"
    # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
    response=$(curl -s "${artifactsUrl}")
    downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')

    if [ -z "${downloadUrl}" ]; then
        buildStatus=$(echo "$response" | jq -r '.message // "Artifact not yet available"')
        echo " Status: ${buildStatus} (elapsed: ${STARTED}s / ${TIMEOUT}s)"
    fi

    sleep 100
    (( STARTED += 100 ))
done
(( STARTED < TIMEOUT ))

if [ -z "${downloadUrl}" ]; then
  echo "ERROR: No downloadUrl found after 40 minutes for commit '$CI_COMMIT_SHA' on branch '$branchName'"
  echo "Last API response:"
  echo "$response" | jq '.'
  echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
  exit 1
fi

echo "Downloading artifacts from: ${downloadUrl}"
curl -o $target_dir/artifacts.zip "$downloadUrl"
unzip $target_dir/artifacts.zip -d $target_dir
mv $target_dir/$artifactName/* $target_dir
rm -rf $target_dir/artifacts.zip
rmdir $target_dir/$artifactName

ls -l $target_dir