#!/bin/bash
set -eo pipefail

echo "Dumping environment variables"
export

sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "GITHUB_SHA=$GITHUB_SHA"
echo "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI=$GITHUB_REPOSITORY"
echo "BUILD_REPOSITORY_URI=$BUILD_REPOSITORY_URI"

repo="$GITHUB_REPOSITORY"
commit_sha="$GITHUB_SHA"

if [ -z "$repo" ]; then
    repo="$BUILD_REPOSITORY_URI"
fi

echo "Using repo=$repo commit=$commit_sha"

repository="--application.source.repository https://github.com/$repo"
commit="--application.source.branchOrCommit #$commit_sha"

if [ "$1" = "windows" ]; then
    echo "Running windows throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile windows --json baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_windows.json"
    rm baseline_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile windows --json profiler_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_windows.json"
    rm profiler_windows.json

elif [ "$1" = "linux" ]; then
    echo "Running Linux  x64 throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux --json baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_linux.json"
    rm baseline_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile linux --json profiler_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_linux.json"
    rm profiler_linux.json

else
    echo "Unknown argument $1"
    exit 1
fi
