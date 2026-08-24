#!/bin/bash
#
# This scripts downloads the necessary binaries to be used for the AWS Lambda Layer.
# This artifacts include: Tracer and ClrProfiler
#

set -eo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
source "$SCRIPT_DIR/download-azure-artifacts-helper.sh"

# Create a directory to store the files
target_dir=artifacts
mkdir -p $target_dir

if [ -n "$CI_COMMIT_TAG" ] && [ -n "$CI_COMMIT_SHA" ]; then
  # Release pipeline
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

# Standard build pipeline
artifactName="serverless-artifacts"

download_azure_artifacts_from_one_build "$target_dir" "$artifactName"
flatten_azure_artifact "$target_dir" "$artifactName"

ls -l $target_dir
