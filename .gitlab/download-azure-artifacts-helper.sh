#!/bin/bash
#
# Shared helpers for locating an Azure DevOps build for the current commit and downloading its
# published artifacts. Used by download-serverless-artifacts.sh, download-single-step-artifacts.sh
# and download-nuget-packages-to-sign.sh: each of those needs a different artifact (or set of
# artifacts) from the build and has its own fallback/post-download logic, but the "find the build,
# then poll it for an artifact" core is identical.
#
# This file is sourced by other scripts, not executed directly:
#   SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
#   source "$SCRIPT_DIR/download-azure-artifacts-helper.sh"
#
# Provides:
#   resolve_azure_build_id                                  - sets the global `buildId`, or exits 1
#   download_azure_artifact <buildId> <artifactName> <dir>   - polls for and unzips one artifact
#                                                               into <dir>/<artifactName>, or exits 1
#   flatten_azure_artifact <dir> <artifactName>               - moves <dir>/<artifactName>/* up into
#                                                               <dir> and removes the now-empty folder

AZDO_BUILD_DEFINITION=54
ARTIFACT_TIMEOUT=2400 # 40 minutes
ARTIFACT_POLL_INTERVAL=100

# Resolves the Azure DevOps build id for $CI_COMMIT_SHA / $CI_COMMIT_BRANCH and sets the global
# `buildId`. Exits 1 if no build can be found.
#
# Prefer the build that was actually triggered for this exact (branch, commit). Azure DevOps
# builds can be parameterized (e.g. debug mode), so two builds of the same commit are not
# necessarily identical — so we match from most-specific to least-specific, and only broaden
# when the precise build can't be found:
#   1. the PR build for this branch + commit (most likely a "full" build)
#   2. a standalone (manual/individualCI) build for this branch + commit
#   3. as a last resort, ANY build carrying this commit on any branch. This rescues pipelines
#      that the GitLab mirror attributed to a ref that merely *contains* the commit (e.g. a
#      feature branch rebased onto this master commit), where no (branch, SHA) build exists.
resolve_azure_build_id() {
  local branchName="refs/heads/$CI_COMMIT_BRANCH"

  echo "Looking for an azure devops build for commit '$CI_COMMIT_SHA' (branch '$branchName') to start"

  # 1. PR build for this branch + commit
  local allBuildsForPrUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=100&queryOrder=queueTimeDescending&reasonFilter=pullRequest"
  buildId=$(curl -sS $allBuildsForPrUrl | jq --arg version $CI_COMMIT_SHA --arg branch $CI_COMMIT_BRANCH '.value[] | select(.triggerInfo["pr.sourceBranch"] == $branch and .triggerInfo["pr.sourceSha"] == $version)  | .id' | head -n 1)

  # 2. Standalone (manual/individualCI) build for this branch + commit
  if [ -z "${buildId}" ]; then
    echo "No PR build found for commit '$CI_COMMIT_SHA' on branch '$branchName'. Checking for standalone builds..."
    local allBuildsForBranchUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=10&queryOrder=queueTimeDescending&branchName=$branchName&reasonFilter=manual,individualCI"
    buildId=$(curl -sS $allBuildsForBranchUrl | jq --arg version $CI_COMMIT_SHA '.value[] | select(.sourceVersion == $version and .reason != "schedule")  | .id' | head -n 1)
  fi

  # 3. Last resort: any build carrying this commit, regardless of branch (prefer non-scheduled).
  # Unlike tiers 1-2 (which intentionally wait on an in-progress build for this exact branch),
  # tier 3's commit is already built elsewhere, so we prefer a completed-successful build to avoid
  # locking onto a queued/canceled/failed newer build and polling it for 40 minutes; we still fall
  # back to an in-progress build if no successful one is found.
  if [ -z "${buildId}" ]; then
    echo "No build found on branch '$branchName' for commit '$CI_COMMIT_SHA'. Falling back to any build carrying this commit..."
    local allBuildsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=200&queryOrder=queueTimeDescending"
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
}

# Polls for the named artifact on the given build for up to $ARTIFACT_TIMEOUT seconds, then
# downloads and unzips it into <targetDir>/<artifactName>. Exits 1 on timeout.
download_azure_artifact() {
  local buildId="$1"
  local artifactName="$2"
  local targetDir="$3"

  echo ""
  echo "=== Downloading artifact '$artifactName' ==="

  local artifactsUrl="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$buildId/artifacts?api-version=7.1&artifactName=$artifactName"

  # Keep trying to get the artifact for 40 minutes
  local downloadUrl=""
  local response=""
  local STARTED=0
  until (( STARTED == ARTIFACT_TIMEOUT )) || [ ! -z "${downloadUrl}" ] ; do
      echo "Checking for artifacts at: ${artifactsUrl}"
      # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
      response=$(curl -s "${artifactsUrl}")
      downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')

      if [ -z "${downloadUrl}" ]; then
          local buildStatus
          buildStatus=$(echo "$response" | jq -r '.message // "Artifact not yet available"')
          echo "  Status: ${buildStatus} (elapsed: ${STARTED}s / ${ARTIFACT_TIMEOUT}s)"

          sleep "$ARTIFACT_POLL_INTERVAL"
          (( STARTED += ARTIFACT_POLL_INTERVAL ))
      fi
  done

  if [ -z "${downloadUrl}" ]; then
    echo "ERROR: No downloadUrl found after 40 minutes for artifact '$artifactName' (commit '$CI_COMMIT_SHA' on branch 'refs/heads/$CI_COMMIT_BRANCH')"
    echo "Last API response:"
    echo "$response" | jq '.'
    echo ""
    echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
    exit 1
  fi

  echo "Downloading artifact '$artifactName' from ${downloadUrl}"
  curl -o "$targetDir/artifact.zip" "$downloadUrl"
  unzip -o "$targetDir/artifact.zip" -d "$targetDir"
  rm -f "$targetDir/artifact.zip"
}

# Moves the contents of <targetDir>/<artifactName>/ up into <targetDir> and removes the now-empty
# <artifactName> folder.
flatten_azure_artifact() {
  local targetDir="$1"
  local artifactName="$2"

  mv "$targetDir/$artifactName"/* "$targetDir"
  rmdir "$targetDir/$artifactName"
}
