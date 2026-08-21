#!/bin/bash

set -eo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
source "$SCRIPT_DIR/download-azure-artifacts-helper.sh"

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

artifactName="ssi-artifacts"

buildId=""
resolve_azure_build_id
download_azure_artifact "$buildId" "$artifactName" "$target_dir"
flatten_azure_artifact "$target_dir" "$artifactName"

ls -l $target_dir
