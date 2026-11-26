#!/usr/bin/env bash

set -ev

organization="datadoghq"
project="dd-trace-dotnet"
personalAccessToken="test"

mkdir -p /app/candidate /app/baseline

artifactNames=("linux-monitoring-home-linux-x64" "Datadog.Trace.Manual.dll" "windows-tracer-home" "linux-monitoring-home-linux-arm64")

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

git clone https://github.com/DataDog/dd-trace-dotnet.git
cd dd-trace-dotnet
echo "Current directory for dd-trace-dotnet: $(pwd)"

git checkout "$branchName"

mkdir -p ./temp-artifacts
echo "Using buildId: $buildId"

# Loop over each artifactName and perform the operations
for artifactName in "${artifactNames[@]}"; do
    echo "Fetching artifact data for $artifactName..."
    artifactData=$(curl -s -u ":$personalAccessToken"   --header "Content-Type: application/json"   "https://dev.azure.com/$organization/$project/_apis/build/builds/$buildId/artifacts?artifactName=$artifactName&api-version=4.1")
    downloadUrl=$(echo "$artifactData" | jq -r '.resource.downloadUrl')
    echo "Downloading artifact $artifactName from $downloadUrl"
    curl -L -o "./temp-artifacts/${artifactName}.zip" "$downloadUrl"
    unzip "./temp-artifacts/${artifactName}.zip" -d "./temp-artifacts/"
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
