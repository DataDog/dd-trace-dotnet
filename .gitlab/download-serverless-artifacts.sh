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

echo "Looking for an azure devops build for commit '$CI_COMMIT_SHA' (branch '$branchName') to start"

# Artifacts are identified by the commit SHA, not the branch: the same SHA always produces
# the same artifacts regardless of which ref the pipeline ran on. A branch-keyed lookup
# breaks when the mirrored GitLab pipeline is attributed to a ref that merely *contains*
# this commit (e.g. a feature branch rebased onto this master commit) — no Azure build
# exists for that (branch, SHA) pair even though builds for the SHA exist on another branch.
# So match on the commit SHA first, across all branches. A build "carries" the commit when
# it is a PR build with pr.sourceSha == SHA, or any build with sourceVersion == SHA. Prefer
# non-scheduled builds, but fall back to a scheduled build for the same SHA.
allBuildsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=200&queryOrder=queueTimeDescending"
buildId=$(curl -sS "$allBuildsUrl" | jq -r --arg version "$CI_COMMIT_SHA" '
  [ .value[] | select((.triggerInfo["pr.sourceSha"] == $version) or (.sourceVersion == $version)) ]
  | ( map(select(.reason != "schedule")) + map(select(.reason == "schedule")) )
  | .[0].id // empty')

if [ -z "${buildId}" ]; then
  echo "No build found for commit '$CI_COMMIT_SHA' (branch '$branchName') in the recent build history"
  exit 1
fi

echo "Found build with id '$buildId' for commit '$CI_COMMIT_SHA'"

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