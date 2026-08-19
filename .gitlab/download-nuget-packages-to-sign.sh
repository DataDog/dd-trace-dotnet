#!/bin/bash
#
# Downloads the NuGet packages built by Azure DevOps that need their contents Authenticode-signed
# in GitLab: dd-trace (the dotnet tool), Datadog.Trace.Bundle and Datadog.AzureFunctions. These
# packages embed the full monitoring home, which Azure DevOps assembles by fanning in per-platform
# artifacts that can only be built there - so unlike the other 5 NuGet packages (which GitLab builds
# and signs itself), these 3 have to be pulled in as a finished .nupkg and signed in place.
#
# This is a parameterised copy of download-single-step-artifacts.sh, generalised to fetch several
# named artifacts from the same Azure DevOps build instead of one. It intentionally does not
# replicate that script's CI_COMMIT_TAG/GitHub-release fallback path: this job always resolves a
# live Azure DevOps build for the current commit, on every pipeline.

set -eo pipefail

target_dir=packages-to-sign
mkdir -p $target_dir

branchName="refs/heads/$CI_COMMIT_BRANCH"
artifactNames=("runner-dotnet-tool" "bundle-nuget-package" "azurefunctions-nuget-package")

echo "Looking for an azure devops build for commit '$CI_COMMIT_SHA' (branch '$branchName') to start"

# Prefer the build that was actually triggered for this exact (branch, commit). Azure DevOps
# builds can be parameterized (e.g. debug mode), so two builds of the same commit are not
# necessarily identical — and this job publishes the "real" artifacts, so we match from
# most-specific to least-specific, and only broaden when the precise build can't be found:
#   1. the PR build for this branch + commit (most likely a "full" build)
#   2. a standalone (manual/individualCI) build for this branch + commit
#   3. as a last resort, ANY build carrying this commit on any branch. This rescues pipelines
#      that the GitLab mirror attributed to a ref that merely *contains* the commit (e.g. a
#      feature branch rebased onto this master commit), where no (branch, SHA) build exists.

# 1. PR build for this branch + commit
allBuildsForPrUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=100&queryOrder=queueTimeDescending&reasonFilter=pullRequest"
buildId=$(curl -sS $allBuildsForPrUrl | jq --arg version $CI_COMMIT_SHA --arg branch $CI_COMMIT_BRANCH '.value[] | select(.triggerInfo["pr.sourceBranch"] == $branch and .triggerInfo["pr.sourceSha"] == $version)  | .id' | head -n 1)

# 2. Standalone (manual/individualCI) build for this branch + commit
if [ -z "${buildId}" ]; then
  echo "No PR build found for commit '$CI_COMMIT_SHA' on branch '$branchName'. Checking for standalone builds..."
  allBuildsForBranchUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=10&queryOrder=queueTimeDescending&branchName=$branchName&reasonFilter=manual,individualCI"
  buildId=$(curl -sS $allBuildsForBranchUrl | jq --arg version $CI_COMMIT_SHA '.value[] | select(.sourceVersion == $version and .reason != "schedule")  | .id' | head -n 1)
fi

# 3. Last resort: any build carrying this commit, regardless of branch (prefer non-scheduled).
# Unlike tiers 1-2 (which intentionally wait on an in-progress build for this exact branch),
# tier 3's commit is already built elsewhere, so we prefer a completed-successful build to avoid
# locking onto a queued/canceled/failed newer build and polling it for 40 minutes; we still fall
# back to an in-progress build if no successful one is found.
if [ -z "${buildId}" ]; then
  echo "No build found on branch '$branchName' for commit '$CI_COMMIT_SHA'. Falling back to any build carrying this commit..."
  allBuildsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=54&\$top=200&queryOrder=queueTimeDescending"
  buildId=$(curl -sS "$allBuildsUrl" | jq -r --arg version "$CI_COMMIT_SHA" '
    [ .value[] | select((.triggerInfo["pr.sourceSha"] == $version) or (.sourceVersion == $version)) ]
    | ( map(select(.reason != "schedule" and .result == "succeeded"))
      + map(select(.reason == "schedule"  and .result == "succeeded"))
      + map(select(.reason != "schedule"))
      + map(select(.reason == "schedule")) )
    | .[0].id // empty')
fi

if [ -z "${buildId}" ]; then
  echo "No build found for commit '$CI_COMMIT_SHA' (branch '$branchName') in the recent build history"
  exit 1
fi

echo "Found build with id '$buildId' for commit '$CI_COMMIT_SHA'"

# Download each artifact in turn. They're all produced by the same Azure DevOps build's
# `dotnet_tool` stage, but stages finish at different times, so each gets its own poll loop.
for artifactName in "${artifactNames[@]}"; do
  echo ""
  echo "=== Downloading artifact '$artifactName' ==="

  artifactsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$buildId/artifacts?api-version=7.1&artifactName=$artifactName"

  # Keep trying to get the artifact for 40 minutes
  downloadUrl=""
  TIMEOUT=2400
  STARTED=0
  until (( STARTED == TIMEOUT )) || [ ! -z "${downloadUrl}" ] ; do
      echo "Checking for artifacts at: ${artifactsUrl}"
      # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
      response=$(curl -s "${artifactsUrl}")
      downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')

      if [ -z "${downloadUrl}" ]; then
          buildStatus=$(echo "$response" | jq -r '.message // "Artifact not yet available"')
          echo "  Status: ${buildStatus} (elapsed: ${STARTED}s / ${TIMEOUT}s)"
      fi

      sleep 100
      (( STARTED += 100 ))
  done
  (( STARTED < TIMEOUT ))

  if [ -z "${downloadUrl}" ]; then
    echo "ERROR: No downloadUrl found after 40 minutes for artifact '$artifactName' (commit '$CI_COMMIT_SHA' on branch '$branchName')"
    echo "Last API response:"
    echo "$response" | jq '.'
    echo ""
    echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
    exit 1
  fi

  echo "Downloading artifact '$artifactName' from ${downloadUrl}"
  curl -o $target_dir/artifact.zip "$downloadUrl"
  unzip -o $target_dir/artifact.zip -d $target_dir
  rm -f $target_dir/artifact.zip
done

# Flatten every .nupkg found (regardless of which artifact/subfolder it came from) directly into
# $target_dir, so the sign-nuget-packages job can copy it straight into ArtifactsDirectory/"packages-to-sign".
find $target_dir -mindepth 2 -name '*.nupkg' -exec mv -t $target_dir {} +
find $target_dir -mindepth 1 -type d -exec rm -rf {} + 2>/dev/null || true

echo ""
echo "Packages to sign:"
ls -l $target_dir
