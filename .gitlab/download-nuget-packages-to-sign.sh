#!/bin/bash
#
# Downloads the NuGet packages built by Azure DevOps that need their contents Authenticode-signed
# in GitLab: dd-trace (the dotnet tool), Datadog.Trace.Bundle and Datadog.AzureFunctions. These
# packages embed the full monitoring home, which Azure DevOps assembles by fanning in per-platform
# artifacts that can only be built there - so unlike the other 5 NuGet packages (which GitLab builds
# and signs itself), these 3 have to be pulled in as a finished .nupkg and signed in place.
#
# Shares its Azure DevOps build-resolution and artifact-polling logic with
# download-single-step-artifacts.sh and download-serverless-artifacts.sh via
# download-azure-artifacts-helper.sh. Unlike those two, it intentionally does not replicate their
# CI_COMMIT_TAG/GitHub-release fallback path: this job always resolves a live Azure DevOps build
# for the current commit, on every pipeline.

set -eo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
source "$SCRIPT_DIR/download-azure-artifacts-helper.sh"

target_dir=packages-to-sign
mkdir -p $target_dir

artifactNames=("runner-dotnet-tool" "bundle-nuget-package" "azurefunctions-nuget-package")
download_azure_artifacts_from_one_build "$target_dir" "${artifactNames[@]}"

# Flatten every .nupkg found (regardless of which artifact/subfolder it came from) directly into
# $target_dir, so the sign-nuget-packages job can copy it straight into ArtifactsDirectory/"packages-to-sign".
find $target_dir -mindepth 2 -name '*.nupkg' -exec mv -t $target_dir {} +
find $target_dir -mindepth 1 -type d -exec rm -rf {} + 2>/dev/null || true

echo ""
echo "Packages to sign:"
ls -l $target_dir
