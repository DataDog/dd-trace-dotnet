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
#   resolve_azure_build_id                                    - sets the global `AZDO_BUILD_ID`, or
#                                                                 exits 1 if no untried build for
#                                                                 the commit can be found
#   download_azure_artifact <buildId> <artifactName> <dir>    - polls for and unzips one artifact
#                                                                 into <dir>/<artifactName>.
#                                                                 Returns 0 on success,
#                                                                 $AZDO_ARTIFACT_UNAVAILABLE if the
#                                                                 build finished without publishing
#                                                                 it, or 1 on a genuine timeout or
#                                                                 an unrecoverable download/unzip
#                                                                 failure (retried a few times first).
#                                                                 Never exits and never changes
#                                                                 `AZDO_BUILD_ID` itself.
#   download_azure_artifacts_from_one_build <dir> <name>...   - the caller-facing entry point:
#                                                                 downloads every named artifact
#                                                                 from a single build, discarding
#                                                                 and retrying against a different
#                                                                 build of the same commit if the
#                                                                 chosen one turns out unable to
#                                                                 publish one of them. Exits 1 if
#                                                                 no build can supply all of them.
#   flatten_azure_artifact <dir> <artifactName>               - moves <dir>/<artifactName>/* up into
#                                                                 <dir> and removes the now-empty
#                                                                 folder

AZDO_API="https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build"
AZDO_BUILD_DEFINITION=54
ARTIFACT_TIMEOUT=2400 # 40 minutes
ARTIFACT_POLL_INTERVAL=100
AZDO_BUILD_STATE_POLL_INTERVAL=300 # only re-check build status every 3rd artifact-poll tick
AZDO_ARTIFACT_UNAVAILABLE=2 # download_azure_artifact: build finished, artifact never published
AZDO_MAX_BUILD_ATTEMPTS=3
AZDO_DOWNLOAD_ATTEMPTS=3 # retries for the final curl+unzip step, unrelated to the polling timeout above
AZDO_TRIED_BUILDS="" # space-separated build ids resolve_azure_build_id should no longer offer

# Curls $1 (an Azure DevOps builds-list URL) and echoes the response, or a `{"value":[]}`
# placeholder (with a warning to stderr) if the request itself failed - so a transient network
# error is treated as "no builds found" for this tier and falls through to the next one, instead
# of aborting the whole script via `set -o pipefail`.
_azure_curl_builds_or_empty() {
  local url="$1"
  local response
  if ! response=$(curl -sS "$url"); then
    echo "  WARNING: failed to query Azure DevOps builds (network error) - treating as no results for this tier" >&2
    echo '{"value":[]}'
    return
  fi
  echo "$response"
}

# Echoes the first id on stdin that isn't in $AZDO_TRIED_BUILDS. Always returns 0, even when every
# candidate is excluded and nothing is echoed, so it never trips `set -o pipefail` as the LAST stage
# of the `AZDO_BUILD_ID=$(... | _azure_first_untried)` pipelines in resolve_azure_build_id. The
# curl/jq stages ahead of it in those pipelines are a separate concern - see
# _azure_curl_builds_or_empty above, which protects those.
_azure_first_untried() {
  local id
  while IFS= read -r id; do
    [ -z "$id" ] && continue
    case " $AZDO_TRIED_BUILDS " in
      *" $id "*) continue ;;
    esac
    echo "$id"
    return 0
  done
  return 0
}

# Resolves the Azure DevOps build id for $CI_COMMIT_SHA / $CI_COMMIT_BRANCH and sets the global
# `AZDO_BUILD_ID`, skipping any build already listed in $AZDO_TRIED_BUILDS. Exits 1 if no such build
# can be found.
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
  AZDO_BUILD_ID=$(_azure_curl_builds_or_empty "$allBuildsForPrUrl" | jq -r --arg version "$CI_COMMIT_SHA" --arg branch "$CI_COMMIT_BRANCH" '.value[] | select(.triggerInfo["pr.sourceBranch"] == $branch and .triggerInfo["pr.sourceSha"] == $version)  | .id' | _azure_first_untried)

  # 2. Standalone (manual/individualCI) build for this branch + commit
  if [ -z "${AZDO_BUILD_ID}" ]; then
    echo "No PR build found for commit '$CI_COMMIT_SHA' on branch '$branchName'. Checking for standalone builds..."
    local allBuildsForBranchUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=10&queryOrder=queueTimeDescending&branchName=$branchName&reasonFilter=manual,individualCI"
    AZDO_BUILD_ID=$(_azure_curl_builds_or_empty "$allBuildsForBranchUrl" | jq -r --arg version "$CI_COMMIT_SHA" '.value[] | select(.sourceVersion == $version and .reason != "schedule")  | .id' | _azure_first_untried)
  fi

  # 3. Last resort: any build carrying this commit, regardless of branch (prefer non-scheduled).
  # Unlike tiers 1-2 (which intentionally wait on an in-progress build for this exact branch),
  # tier 3's commit is already built elsewhere, so we prefer a completed-successful build to avoid
  # locking onto a queued/canceled/failed newer build and polling it for 40 minutes; we still fall
  # back to an in-progress build if no successful one is found.
  if [ -z "${AZDO_BUILD_ID}" ]; then
    echo "No build found on branch '$branchName' for commit '$CI_COMMIT_SHA'. Falling back to any build carrying this commit..."
    local allBuildsUrl="${AZDO_API}/builds?api-version=7.1&definitions=${AZDO_BUILD_DEFINITION}&\$top=200&queryOrder=queueTimeDescending"
    AZDO_BUILD_ID=$(_azure_curl_builds_or_empty "$allBuildsUrl" | jq -r --arg version "$CI_COMMIT_SHA" '
      [ .value[] | select((.triggerInfo["pr.sourceSha"] == $version) or (.sourceVersion == $version)) ]
      | ( map(select(.reason != "schedule" and .result == "succeeded"))
        + map(select(.reason == "schedule"  and .result == "succeeded"))
        + map(select(.reason != "schedule"))
        + map(select(.reason == "schedule")) )
      | .[].id' | _azure_first_untried)
  fi

  if [ -z "${AZDO_BUILD_ID}" ]; then
    if [ -n "$AZDO_TRIED_BUILDS" ]; then
      echo "No usable build found for commit '$CI_COMMIT_SHA' (branch '$branchName'): already tried and rejected build(s):$AZDO_TRIED_BUILDS"
    else
      echo "No build found for commit '$CI_COMMIT_SHA' (branch '$branchName') in the recent build history"
    fi
    exit 1
  fi

  echo "Found build with id '$AZDO_BUILD_ID' for commit '$CI_COMMIT_SHA'"
}

# Sets the globals AZDO_BUILD_STATUS / AZDO_BUILD_RESULT for the given build.
azure_build_state() {
  local buildId="$1"
  local response
  if ! response=$(curl -fsS "${AZDO_API}/builds/${buildId}?api-version=7.1"); then
    echo "  WARNING: failed to query status for build $buildId" >&2
    AZDO_BUILD_STATUS="unknown"
    AZDO_BUILD_RESULT="unknown"
    return
  fi
  AZDO_BUILD_STATUS=$(jq -r '.status // "unknown"' <<<"$response" 2>/dev/null || echo "unknown")
  AZDO_BUILD_RESULT=$(jq -r '.result // "none"' <<<"$response" 2>/dev/null || echo "none")
}

# Fetches the artifacts-list response for $1, or echoes an empty string (with a warning to stderr)
# on a hard curl failure. HTTP error status codes are NOT curl failures here (no `-f`): Azure
# returns a normal JSON body with no `resource` when the named artifact doesn't exist yet, and the
# ordinary "not published yet" polling case relies on that body being readable.
_azure_fetch_artifacts_response() {
  local url="$1"
  local response
  if ! response=$(curl -sS "$url"); then
    echo "  WARNING: failed to query artifacts at $url (network error)" >&2
    echo ""
    return
  fi
  echo "$response"
}

# Polls for the named artifact on the given build for up to $ARTIFACT_TIMEOUT seconds, then
# downloads and unzips it into <targetDir>/<artifactName>.
#
# Returns:
#   0                          - downloaded successfully
#   $AZDO_ARTIFACT_UNAVAILABLE - the build has finished and will never publish this artifact
#                                (build `result` alone is NOT used for this: failed builds
#                                routinely still publish every artifact, so only
#                                "status == completed and the artifact still isn't there" counts)
#   1                          - genuine timeout (the build is still running after $ARTIFACT_TIMEOUT)
#                                or the download/unzip step failed after $AZDO_DOWNLOAD_ATTEMPTS retries
#
# Never calls `exit` and never reassigns `AZDO_BUILD_ID` - switching to a different build is entirely
# up to the caller (see download_azure_artifacts_from_one_build below).
download_azure_artifact() {
  local buildId="$1"
  local artifactName="$2"
  local targetDir="$3"

  echo ""
  echo "=== Downloading artifact '$artifactName' ==="

  local artifactsUrl="${AZDO_API}/builds/${buildId}/artifacts?api-version=7.1&artifactName=${artifactName}"
  local downloadUrl="" response="" elapsed=0
  AZDO_BUILD_STATUS="unknown"

  while true; do
    echo "Checking for artifacts at: ${artifactsUrl}"
    # If the artifact doesn't exist, .resource.downloadUrl will be null, so we filter that out
    response=$(_azure_fetch_artifacts_response "${artifactsUrl}")
    downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')

    if [ -n "${downloadUrl}" ]; then
      break
    fi

    # Checking build status is a second API call, so only do it every 3rd tick (~5 minutes) instead
    # of every tick - except the very first, so an already-completed build is caught immediately.
    if (( elapsed % AZDO_BUILD_STATE_POLL_INTERVAL == 0 )); then
      azure_build_state "$buildId"
    fi
    if [ "$AZDO_BUILD_STATUS" = "completed" ]; then
      # The status read above and the artifact publish can race - re-check once before giving up.
      response=$(_azure_fetch_artifacts_response "${artifactsUrl}")
      downloadUrl=$(echo "$response" | jq -r '.resource.downloadUrl | select( . != null )')
      if [ -n "${downloadUrl}" ]; then
        break
      fi

      echo "  Build $buildId finished as '$AZDO_BUILD_RESULT' without publishing '$artifactName'"
      return "$AZDO_ARTIFACT_UNAVAILABLE"
    fi

    if (( elapsed >= ARTIFACT_TIMEOUT )); then
      echo "ERROR: No downloadUrl found after ${ARTIFACT_TIMEOUT}s for artifact '$artifactName' (commit '$CI_COMMIT_SHA' on branch 'refs/heads/$CI_COMMIT_BRANCH')"
      echo "Last API response:"
      echo "$response" | jq '.'
      echo ""
      echo "Build URL: https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=$buildId"
      return 1
    fi

    echo "  Waiting for '$artifactName' - build $buildId is $AZDO_BUILD_STATUS (elapsed: ${elapsed}s / ${ARTIFACT_TIMEOUT}s)"
    sleep "$ARTIFACT_POLL_INTERVAL"
    (( elapsed += ARTIFACT_POLL_INTERVAL ))
  done

  echo "Downloading artifact '$artifactName' from ${downloadUrl}"
  # download_azure_artifact runs with `set -e` suppressed (it's invoked as `if download_azure_artifact
  # ...; then` by its caller), so every command below must check its own exit status explicitly.
  # A curl/unzip failure here is a one-off transient error (network blip, truncated write), not a
  # build problem, so retry a few times before giving up - unlike the poll loop above, this is not
  # a "genuine timeout" and doesn't warrant discarding the build and starting over.
  local downloadAttempt
  for (( downloadAttempt = 1; downloadAttempt <= AZDO_DOWNLOAD_ATTEMPTS; downloadAttempt++ )); do
    if curl -fsS -o "$targetDir/artifact.zip" "$downloadUrl" && unzip -o "$targetDir/artifact.zip" -d "$targetDir"; then
      rm -f "$targetDir/artifact.zip"
      return 0
    fi
    echo "  WARNING: failed to download/unzip '$artifactName' (attempt $downloadAttempt/$AZDO_DOWNLOAD_ATTEMPTS)" >&2
    rm -f "$targetDir/artifact.zip"
    (( downloadAttempt < AZDO_DOWNLOAD_ATTEMPTS )) && sleep 5
  done

  echo "ERROR: failed to download/unzip artifact '$artifactName' after $AZDO_DOWNLOAD_ATTEMPTS attempts" >&2
  return 1
}

# Downloads every named artifact from a SINGLE Azure DevOps build into <targetDir>/<artifactName>
# each. If that build turns out never to publish one of them, discards everything downloaded for
# this attempt and starts over against a different build of the same commit (bounded by
# $AZDO_MAX_BUILD_ATTEMPTS) - artifacts from two different builds are never mixed together. On
# return, the global `AZDO_BUILD_ID` holds the build every artifact actually came from.
#
# resolve_azure_build_id exits 1 as soon as it has no untried build left to offer, so "there's no
# better build available" fails fast instead of waiting out a doomed poll.
download_azure_artifacts_from_one_build() {
  local targetDir="$1"
  shift
  local artifactNames=("$@")

  local attempt
  for (( attempt = 1; attempt <= AZDO_MAX_BUILD_ATTEMPTS; attempt++ )); do
    AZDO_BUILD_ID=""
    resolve_azure_build_id

    local artifactName ok=1 rc
    for artifactName in "${artifactNames[@]}"; do
      if download_azure_artifact "$AZDO_BUILD_ID" "$artifactName" "$targetDir"; then
        rc=0
      else
        rc=$?
      fi

      if [ "$rc" -eq 0 ]; then
        continue
      fi

      if [ "$rc" -eq "$AZDO_ARTIFACT_UNAVAILABLE" ]; then
        ok=0
        break
      fi

      # Genuine timeout, or a download/unzip failure that survived retries - not recoverable by
      # trying a different build.
      exit 1
    done

    if [ "$ok" -eq 1 ]; then
      return 0
    fi

    echo "Discarding partial downloads from build $AZDO_BUILD_ID and trying a different build (attempt $attempt/$AZDO_MAX_BUILD_ATTEMPTS)"
    AZDO_TRIED_BUILDS="$AZDO_TRIED_BUILDS $AZDO_BUILD_ID"
    find "${targetDir:?}" -mindepth 1 -delete
  done

  echo "ERROR: exhausted $AZDO_MAX_BUILD_ATTEMPTS build attempt(s) for commit '$CI_COMMIT_SHA' without obtaining: ${artifactNames[*]}"
  exit 1
}

# Moves the contents of <targetDir>/<artifactName>/ up into <targetDir> and removes the now-empty
# <artifactName> folder.
flatten_azure_artifact() {
  local targetDir="$1"
  local artifactName="$2"

  mv "$targetDir/$artifactName"/* "$targetDir"
  rmdir "$targetDir/$artifactName"
}
