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
branchOrCommit="$GITHUB_HEAD_REF"

if [ -z "$repo" ]; then
    repo="$BUILD_REPOSITORY_URI"
fi

if [ -z "$GITHUB_HEAD_REF" ]; then
    branchOrCommit="#$GITHUB_SHA"
fi

echo "Using repo=$repo commit=$commit_sha"

repository="--application.source.repository https://github.com/$repo"
commit="--application.source.branchOrCommit $branchOrCommit"

if [ "$1" = "windows" ]; then
    echo "Running windows throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_baseline --profile windows --json profiler_baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=profiler_baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_baseline_windows.json"
    rm profiler_baseline_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile windows --json profiler_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_windows.json"
    rm profiler_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions_baseline --profile windows --json profiler_exceptions_baseline_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions_baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_baseline_windows.json"
    rm profiler_exceptions_baseline_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions --profile windows --json profiler_exceptions_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_windows.json"
    rm profiler_exceptions_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_cpu --profile windows --json profiler_cpu_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_cpu --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_cpu_windows.json"
    rm profiler_cpu_windows.json

elif [ "$1" = "linux" ]; then
    echo "Running Linux  x64 throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_baseline --profile linux --json profiler_baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_baseline_linux.json"
    rm profiler_baseline_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile linux --json profiler_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_linux.json"
    rm profiler_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions_baseline --profile linux --json profiler_exceptions_baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions_baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_baseline_linux.json"
    rm profiler_exceptions_baseline_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions --profile linux --json profiler_exceptions_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_linux.json"
    rm profiler_exceptions_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_cpu --profile linux --json profiler_cpu_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_cpu --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_cpu_linux.json"
    rm profiler_cpu_linux.json

else
    echo "Unknown argument $1"
    exit 1
fi
