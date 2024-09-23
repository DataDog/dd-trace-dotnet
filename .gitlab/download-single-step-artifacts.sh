#!/bin/bash

set -eo pipefail

#Create a directory to store the files
target_dir=artifacts
mkdir -p $target_dir

if [ -n "$CI_COMMIT_TAG" ] || [ -n "$DOTNET_PACKAGE_VERSION" ]; then
  echo "Downloading artifacts from Github"
  VERSION=${DOTNET_PACKAGE_VERSION:-${CI_COMMIT_TAG##v}} # Use DOTNET_PACKAGE_VERSION if it exists, otherwise use CI_COMMIT_TAG without the v

  for SUFFIX in "" ".arm64"; do
    curl --location --fail \
      --output $target_dir/datadog-dotnet-apm-${VERSION}${SUFFIX}.tar.gz \
      "https://github.com/DataDog/dd-trace-dotnet/releases/download/v${VERSION}/datadog-dotnet-apm-${VERSION}${SUFFIX}.tar.gz"
  done

  echo -n $VERSION > $target_dir/version.txt
  exit 0
fi

branchName="refs/heads/$CI_COMMIT_BRANCH"
artifactName="ssi-artifacts"

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

# Now try to download the ssi artifacts from the build
artifactsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$buildId/artifacts?api-version=7.1&artifactName=$artifactName"

# Keep trying to get the artifact for 40 minutes
TIMEOUT=2400
STARTED=0
until (( STARTED == TIMEOUT )) || [ ! -z "${downloadUrl}" ] ; do
    echo "Checking for artifacts at '$artifactsUrl'..."
    # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
    downloadUrl=$(curl -s $artifactsUrl | jq -r '.resource.downloadUrl | select( . != null )')
    sleep 100
    (( STARTED += 100 ))
done
(( STARTED < TIMEOUT ))

if [ -z "${downloadUrl}" ]; then
  echo "No downloadUrl found after 30 minutes for commit '$CI_COMMIT_SHA' on branch '$branchName'"
  exit 1
fi

echo "Downloading artifacts from '$downloadUrl'..."
curl -o $target_dir/artifacts.zip "$downloadUrl"
unzip $target_dir/artifacts.zip -d $target_dir
mv $target_dir/$artifactName/* $target_dir
rm -rf $target_dir/artifacts.zip
rmdir $target_dir/$artifactName

ls -l $target_dir