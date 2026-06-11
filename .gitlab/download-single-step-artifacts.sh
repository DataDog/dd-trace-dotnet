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

  if [ -n "$CI_COMMIT_SHA" ]; then
    # Put this in the same place the "build" stage does
    win_target_dir=artifacts-out
    mkdir -p $win_target_dir

    echo "Downloading Windows Tracer Home from Github"
    curl --location --fail \
        --output $win_target_dir/windows-tracer-home.zip \
        "https://github.com/DataDog/dd-trace-dotnet/releases/download/v${VERSION}/windows-tracer-home.zip"

    echo "Downloading Windows fleet-installer from S3"

    curl --location --fail \
        --output $win_target_dir/fleet-installer.zip \
        "https://dd-windowsfilter.s3.amazonaws.com/builds/tracer/${CI_COMMIT_SHA}/fleet-installer.zip"

    echo "Downloading Telemetry Forwarder from S3"

    curl --location --fail \
        --output $win_target_dir/telemetry_forwarder.exe \
        "https://dd-windowsfilter.s3.amazonaws.com/builds/tracer/${CI_COMMIT_SHA}/telemetry_forwarder.exe"
  fi

  echo -n $VERSION > $target_dir/version.txt
  exit 0
fi

branchName="refs/heads/$CI_COMMIT_BRANCH"
artifactName="ssi-artifacts"

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
  echo "No build found carrying commit '$CI_COMMIT_SHA' (branch '$branchName') in the recent build history"
  exit 1
fi

echo "Found build with id '$buildId' for commit '$CI_COMMIT_SHA'"

# Now try to download the ssi artifacts from the build
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
        # Check if the build exists and show status
        buildStatus=$(echo "$response" | jq -r '.message // "Artifact not yet available"')
        echo "  Status: ${buildStatus} (elapsed: ${STARTED}s / ${TIMEOUT}s)"
    fi

    sleep 100
    (( STARTED += 100 ))
done
(( STARTED < TIMEOUT ))

if [ -z "${downloadUrl}" ]; then
  echo "ERROR: No downloadUrl found after 40 minutes for commit '$CI_COMMIT_SHA' on branch '$branchName'"
  echo "Last API response:"
  echo "$response" | jq '.'
  echo ""
  echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
  exit 1
fi

echo "Downloading artifacts from ${downloadUrl}"
curl -o $target_dir/artifacts.zip "$downloadUrl"
unzip $target_dir/artifacts.zip -d $target_dir
mv $target_dir/$artifactName/* $target_dir
rm -rf $target_dir/artifacts.zip
rmdir $target_dir/$artifactName

ls -l $target_dir