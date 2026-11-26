#!/usr/bin/env bash

set -ev

source "$(dirname "${BASH_SOURCE[0]}")/retry.sh"

organization="datadoghq"
project="dd-trace-dotnet"
personalAccessToken="1"

artifactNames=("linux-monitoring-home-linux-x64" "Datadog.Trace.Manual.dll" "windows-tracer-home" "linux-monitoring-home-linux-arm64")
export COR_PROFILER_PATH="${current_dir}/${NATIVE_PROFILER_PATH}"

if [ -z "$CI_REPOSITORY_URL" ]; then
    branchName="master"
    echo "Running locally, setting Branch to default branch $branchName"
else
    if echo "$CI_REPOSITORY_URL" | grep -q "benchmark"; then
        branchName="master"
        echo "Running from Benchmarking repo, setting Branch to default branch $branchName"
    else
        branchName="$CI_COMMIT_REF_NAME"
        echo "Running from dd-trace-dotnet repo, setting Branch to current branch $branchName"
    fi
fi

if [ -e dd-trace-dotnet ]; then
    echo "Cannot clone dd-trace-dotnet: destination already exists" >&2
    exit 1
fi

# Use a fresh directory on every attempt, including after a timed-out clone.
clone_sut() (
    local clone_dir
    clone_dir=$(mktemp -d ./dd-trace-dotnet.XXXXXX) || exit 1
    trap 'rm -rf "$clone_dir"' EXIT
    timeout --kill-after="${CLONE_KILL_AFTER:-5s}" "${CLONE_TIMEOUT:-90s}" git clone https://github.com/DataDog/dd-trace-dotnet.git "$clone_dir" &&
        mv -T "$clone_dir" dd-trace-dotnet
)
retry clone_sut
cd dd-trace-dotnet
git checkout $branchName

#switching the target framework to latest supported variant
sed -i 's/<TargetFramework>net5.0<\/TargetFramework>/<TargetFramework>net9.0<\/TargetFramework>/' ./tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController/Samples.AspNetCoreSimpleController.csproj

mkdir -p ./temp-artifacts

# Loop over each artifactName and perform the operations
for artifactName in "${artifactNames[@]}"; do
    if ! artifactData=$(curl_with_retry - -u ":$personalAccessToken" --header "Content-Type: application/json" "https://dev.azure.com/$organization/$project/_apis/build/builds/$buildId/artifacts?artifactName=$artifactName&api-version=4.1"); then
        echo "Failed to get metadata for artifact $artifactName" >&2
        exit 1
    fi
    downloadUrl=$(echo "$artifactData" | jq -r '.resource.downloadUrl')

    if [ "$downloadUrl" = "null" ] || [ -z "$downloadUrl" ]; then
        echo "Failed to get download URL for artifact $artifactName"
        echo "Artifact data: $artifactData"
        exit 1
    fi

    if ! curl_with_retry "./temp-artifacts/${artifactName}.zip" "$downloadUrl"; then
        echo "Failed to download artifact $artifactName"
        echo "Artifact data: $artifactData"
        echo "Download URL: $downloadUrl"
        exit 1
    fi

    if ! unzip "./temp-artifacts/${artifactName}.zip" -d "./temp-artifacts/"; then
        echo "Failed to unzip artifact $artifactName"
        echo "Artifact data: $artifactData"
        echo "Download URL: $downloadUrl"
        exit 1
    fi
done
mkdir -p ./tracer/tracer-home-linux-arm64
mkdir -p ./tracer/tracer-home-linux
mkdir -p ./tracer/tracer-home-win
mkdir -p tracer/bin/netcoreapp3.1

mv ./temp-artifacts/linux-monitoring-home-linux-arm64/* ./tracer/tracer-home-linux-arm64
mv ./temp-artifacts/linux-monitoring-home-linux-x64/* ./tracer/tracer-home-linux
mv ./temp-artifacts/windows-tracer-home/* ./tracer/tracer-home-win

cp ./temp-artifacts/Datadog.Trace.Manual.dll/* ./tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController/
mv ./temp-artifacts/Datadog.Trace.Manual.dll/* ./tracer/bin/netcoreapp3.1/
