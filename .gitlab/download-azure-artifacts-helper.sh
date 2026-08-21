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
#                                                               into <dir>/<artifactName>. Bails out
#                                                               (after trying to recover onto a
#                                                               different build of the same commit)
#                                                               as soon as Azure DevOps reports the
#                                                               artifact can no longer be produced,
#                                                               rather than always waiting the full
#                                                               timeout. Exits 1 if the artifact
#                                                               can't be obtained.
#   flatten_azure_artifact <dir> <artifactName>               - moves <dir>/<artifactName>/* up into
#                                                               <dir> and removes the now-empty folder
#
# The remaining functions in this file (azure_stage_for_artifact, azure_build_state,
# azure_stage_state, is_azure_artifact_doomed, switch_to_better_azure_build,
# azure_artifact_failure) are implementation details of download_azure_artifact, not a stable
# interface for callers.

AZDO_API="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build"
AZDO_BUILD_DEFINITION=54
ARTIFACT_TIMEOUT=2400 # 40 minutes
ARTIFACT_POLL_INTERVAL=100
STAGE_CHECK_INTERVAL=300 # how often to re-read the (multi-MB) build timeline while polling

declare -A AZ_STAGE_CACHE    # "buildId:stageName" -> "state|result"
declare -A AZ_STAGE_CACHE_AT # "buildId:stageName" -> unix time of the last timeline fetch
AZ_TRIED_BUILDS=""           # space-separated build ids already rejected by switch_to_better_azure_build

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
  local allBuildsForPrUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=100&queryOrder=queueTimeDescending&reasonFilter=pullRequest"
  buildId=$(curl -sS $allBuildsForPrUrl | jq --arg version $CI_COMMIT_SHA --arg branch $CI_COMMIT_BRANCH '.value[] | select(.triggerInfo["pr.sourceBranch"] == $branch and .triggerInfo["pr.sourceSha"] == $version)  | .id' | head -n 1)

  # 2. Standalone (manual/individualCI) build for this branch + commit
  if [ -z "${buildId}" ]; then
    echo "No PR build found for commit '$CI_COMMIT_SHA' on branch '$branchName'. Checking for standalone builds..."
    local allBuildsForBranchUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=10&queryOrder=queueTimeDescending&branchName=$branchName&reasonFilter=manual,individualCI"
    buildId=$(curl -sS $allBuildsForBranchUrl | jq --arg version $CI_COMMIT_SHA '.value[] | select(.sourceVersion == $version and .reason != "schedule")  | .id' | head -n 1)
  fi

  # 3. Last resort: any build carrying this commit, regardless of branch (prefer non-scheduled).
  # Unlike tiers 1-2 (which intentionally wait on an in-progress build for this exact branch),
  # tier 3's commit is already built elsewhere, so we prefer a completed-successful build to avoid
  # locking onto a queued/canceled/failed newer build and polling it for 40 minutes; we still fall
  # back to an in-progress build if no successful one is found.
  if [ -z "${buildId}" ]; then
    echo "No build found on branch '$branchName' for commit '$CI_COMMIT_SHA'. Falling back to any build carrying this commit..."
    local allBuildsUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=200&queryOrder=queueTimeDescending"
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

# Maps an artifact name to the Azure DevOps pipeline stage (see .azure-pipelines/ultimate-pipeline.yml)
# that publishes it, so download_azure_artifact can tell "not ready yet" apart from "never coming".
# Artifacts with no known stage fall back to the whole-build status only (see is_azure_artifact_doomed).
azure_stage_for_artifact() {
  case "$1" in
    ssi-artifacts) echo "store_ssi_artifacts" ;;
    serverless-artifacts) echo "store_serverless_artifacts" ;;
    runner-dotnet-tool | bundle-nuget-package | azurefunctions-nuget-package) echo "dotnet_tool" ;;
    *) echo "" ;;
  esac
}

# Sets the globals AZ_BUILD_STATUS / AZ_BUILD_RESULT for the given build. Cheap (a few KB), so
# unlike azure_stage_state this is not throttled or cached.
azure_build_state() {
  local buildId="$1"
  local response
  response=$(curl -sS "${AZDO_API}/builds/${buildId}?api-version=7.1")
  AZ_BUILD_STATUS=$(echo "$response" | jq -r '.status // "unknown"')
  AZ_BUILD_RESULT=$(echo "$response" | jq -r '.result // "none"')
}

# Sets the globals AZ_STAGE_STATE / AZ_STAGE_RESULT for the named stage's latest attempt on the
# given build, by reading the build timeline. Returns 1 (leaving both globals empty) if the stage
# isn't present in the timeline at all.
#
# The timeline can be several MB (16k+ records on a full build), so this is throttled to at most
# one fetch per $STAGE_CHECK_INTERVAL seconds per (buildId, stageName), and skipped entirely once a
# stage has already been observed to succeed - at that point there's nothing left to learn.
azure_stage_state() {
  local buildId="$1"
  local stageName="$2"
  local cacheKey="${buildId}:${stageName}"
  local cached="${AZ_STAGE_CACHE[$cacheKey]:-}"

  if [ "$cached" = "completed|succeeded" ] || [ "$cached" = "completed|succeededWithIssues" ]; then
    AZ_STAGE_STATE="completed"
    AZ_STAGE_RESULT="${cached##*|}"
    return 0
  fi

  local now lastCheck
  now=$(date +%s)
  lastCheck="${AZ_STAGE_CACHE_AT[$cacheKey]:-0}"
  if [ -n "$cached" ] && (( now - lastCheck < STAGE_CHECK_INTERVAL )); then
    AZ_STAGE_STATE="${cached%%|*}"
    AZ_STAGE_RESULT="${cached##*|}"
    return 0
  fi

  local response stateResult
  response=$(curl -sS --compressed "${AZDO_API}/builds/${buildId}/timeline?api-version=7.1")
  stateResult=$(echo "$response" | jq -r --arg s "$stageName" '
    [ .records[]? | select(.type == "Stage" and .identifier == $s) ]
    | sort_by(.attempt) | last
    | if . == null then "" else "\(.state)|\(.result // "none")" end')

  AZ_STAGE_CACHE_AT[$cacheKey]=$now

  if [ -z "$stateResult" ]; then
    AZ_STAGE_CACHE[$cacheKey]=""
    AZ_STAGE_STATE=""
    AZ_STAGE_RESULT=""
    return 1
  fi

  AZ_STAGE_CACHE[$cacheKey]="$stateResult"
  AZ_STAGE_STATE="${stateResult%%|*}"
  AZ_STAGE_RESULT="${stateResult##*|}"
  return 0
}

# Returns 0 (doomed - the artifact will never appear) if the stage that publishes $artifactName has
# finished without succeeding, or - when that stage can't be resolved, e.g. an unmapped artifact or
# a pipeline rename that made the stage name stale - if the whole build has completed. The whole-
# build check is a deliberately lenient backstop: build `result` (succeeded/failed/canceled) is NOT
# a reliable signal on its own (failed builds routinely still publish every artifact), only
# `status == completed` combined with "the artifact still isn't there" is.
#
# Also refreshes AZ_BUILD_STATUS / AZ_BUILD_RESULT as a side effect, so callers always have current
# values to log even when this returns "not doomed".
is_azure_artifact_doomed() {
  local buildId="$1"
  local stageName="$2"

  azure_build_state "$buildId"

  if [ -n "$stageName" ] && azure_stage_state "$buildId" "$stageName"; then
    if [ "$AZ_STAGE_STATE" = "completed" ]; then
      case "$AZ_STAGE_RESULT" in
        succeeded | succeededWithIssues) return 1 ;;
        *) return 0 ;;
      esac
    fi
    return 1
  fi

  if [ -n "$stageName" ] && [ -z "${AZ_STAGE_WARNED:-}" ]; then
    echo "  WARNING: stage '$stageName' not found in build $buildId's timeline; falling back to whole-build status to decide whether the artifact is still coming"
    AZ_STAGE_WARNED=1
  fi

  [ "$AZ_BUILD_STATUS" = "completed" ]
}

# Looks for a different Azure DevOps build of the same commit that can still supply $artifactName,
# for when $currentBuildId turns out to be doomed - e.g. a newer build was queued for the same
# commit and failed, while an earlier one for that commit already succeeded. Only ever matches
# builds by exact commit SHA (never branch alone), so this can never switch onto artifacts built
# from different code. On success sets the global `buildId` to the replacement and returns 0;
# returns 1 (leaving `buildId` unchanged) if no better candidate exists.
switch_to_better_azure_build() {
  local currentBuildId="$1"
  local artifactName="$2"

  echo "  Build $currentBuildId can no longer produce '$artifactName'. Looking for another build of commit '$CI_COMMIT_SHA'..."
  AZ_TRIED_BUILDS="$AZ_TRIED_BUILDS $currentBuildId"

  local allBuildsUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=200&queryOrder=queueTimeDescending"
  local -a candidates=()
  local candidate
  while IFS= read -r candidate; do
    [ -z "$candidate" ] && continue
    case " $AZ_TRIED_BUILDS " in
      *" $candidate "*) continue ;;
    esac
    candidates+=("$candidate")
  done < <(curl -sS "$allBuildsUrl" | jq -r --arg version "$CI_COMMIT_SHA" '
    [ .value[] | select((.triggerInfo["pr.sourceSha"] == $version) or (.sourceVersion == $version)) ]
    | .[] | select(.status != "completed" or .result == "succeeded" or .result == "partiallySucceeded")
    | .id')

  # Prefer a candidate that already has the artifact, whether it is still running or completed.
  local downloadUrl
  for candidate in "${candidates[@]}"; do
    downloadUrl=$(curl -sS "${AZDO_API}/builds/${candidate}/artifacts?api-version=7.1&artifactName=${artifactName}" | jq -r '.resource.downloadUrl | select( . != null )')
    if [ -n "$downloadUrl" ]; then
      echo "  Switching to build $candidate, which already has '$artifactName'"
      buildId="$candidate"
      return 0
    fi
  done

  # Otherwise, fall back to the first still-running candidate; it may yet publish the artifact.
  for candidate in "${candidates[@]}"; do
    azure_build_state "$candidate"
    if [ "$AZ_BUILD_STATUS" != "completed" ]; then
      echo "  Switching to build $candidate, which is still running"
      buildId="$candidate"
      return 0
    fi
  done

  echo "  No alternative build of commit '$CI_COMMIT_SHA' can supply '$artifactName'"
  return 1
}

# Prints the diagnostics block shared by every failure path (doomed with no recovery, timeout).
azure_artifact_failure() {
  local buildId="$1"
  local artifactName="$2"
  local reason="$3"
  local response="$4"

  echo "ERROR: '$artifactName' will not be downloaded from build $buildId (commit '$CI_COMMIT_SHA' on branch 'refs/heads/$CI_COMMIT_BRANCH')"
  echo "  Reason: $reason"
  echo "Last API response:"
  echo "$response" | jq '.'
  echo ""
  echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
}

# Polls for the named artifact for up to $ARTIFACT_TIMEOUT seconds, then downloads and unzips it
# into <targetDir>/<artifactName>.
#
# As soon as the artifact's publishing stage finishes without succeeding (or, failing that, once
# the whole build completes) with the artifact still absent, this tries to switch to a different
# build of the same commit rather than waiting out the rest of the timeout. Exits 1 if the
# artifact can't be obtained, either because no build can supply it or because of a genuine timeout.
download_azure_artifact() {
  local currentBuildId="$1"
  local artifactName="$2"
  local targetDir="$3"

  echo ""
  echo "=== Downloading artifact '$artifactName' ==="

  local stageName
  stageName=$(azure_stage_for_artifact "$artifactName")

  local artifactsUrl="${AZDO_API}/builds/${currentBuildId}/artifacts?api-version=7.1&artifactName=${artifactName}"
  local downloadUrl="" response="" elapsed=0

  while true; do
    echo "Checking for artifacts at: ${artifactsUrl}"
    # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
    response=$(curl -s "${artifactsUrl}")
    downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')

    if [ -n "${downloadUrl}" ]; then
      break
    fi

    if is_azure_artifact_doomed "$currentBuildId" "$stageName"; then
      # The status read above and the artifact publish can race - re-check once before giving up.
      response=$(curl -s "${artifactsUrl}")
      downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')
      if [ -n "${downloadUrl}" ]; then
        break
      fi

      if switch_to_better_azure_build "$currentBuildId" "$artifactName"; then
        currentBuildId="$buildId"
        artifactsUrl="${AZDO_API}/builds/${currentBuildId}/artifacts?api-version=7.1&artifactName=${artifactName}"
        continue
      fi

      local reason
      if [ -n "$stageName" ] && [ "$AZ_STAGE_STATE" = "completed" ]; then
        reason="build $currentBuildId's stage '$stageName' finished as '$AZ_STAGE_RESULT'"
      else
        reason="build $currentBuildId finished as '$AZ_BUILD_STATUS'/'$AZ_BUILD_RESULT'"
      fi
      azure_artifact_failure "$currentBuildId" "$artifactName" \
        "$reason and no alternative build was found. If the Azure DevOps stage was retried, re-run this job." \
        "$response"
      exit 1
    fi

    if (( elapsed >= ARTIFACT_TIMEOUT )); then
      azure_artifact_failure "$currentBuildId" "$artifactName" \
        "timed out after ${ARTIFACT_TIMEOUT}s waiting for the artifact" \
        "$response"
      exit 1
    fi

    echo "  Waiting for '$artifactName' - build $currentBuildId is $AZ_BUILD_STATUS${AZ_STAGE_STATE:+, stage $stageName is $AZ_STAGE_STATE} (elapsed: ${elapsed}s / ${ARTIFACT_TIMEOUT}s)"
    sleep "$ARTIFACT_POLL_INTERVAL"
    (( elapsed += ARTIFACT_POLL_INTERVAL ))
  done

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
