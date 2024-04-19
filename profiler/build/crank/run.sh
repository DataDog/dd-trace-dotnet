#!/bin/bash
set -eo pipefail

echo "Dumping environment variables"
export

sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "SYSTEM_PULLREQUEST_SOURCECOMMITID=$SYSTEM_PULLREQUEST_SOURCECOMMITID"
echo "BUILD_SOURCEVERSION=$BUILD_SOURCEVERSION"
echo "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI=$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
echo "BUILD_REPOSITORY_URI=$BUILD_REPOSITORY_URI"

repo="$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
commit_sha="$SYSTEM_PULLREQUEST_SOURCECOMMITID"

if [ -z "$repo" ]; then
    repo="$BUILD_REPOSITORY_URI"
fi

if [ -z "$commit_sha" ]; then
    commit_sha="$BUILD_SOURCEVERSION"
fi

echo "Using repo=$repo commit=$commit_sha"

repository="--application.source.repository $repo"
commit="--application.source.branchOrCommit #$commit_sha"

if [ "$1" = "windows" ]; then
    echo "Running windows throughput tests"

    rm -f profiler_baseline_windows.json
    rm -f profiler_windows.json
    rm -f profiler_exceptions_baseline_windows.json
    rm -f profiler_windows_walltime.json
    rm -f profiler_exceptions_windows.json
    rm -f profiler_cpu_windows.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_baseline --profile windows --json profiler_baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=profiler_baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_baseline_windows.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile windows --json profiler_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_windows.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_walltime --profile windows --json profiler_windows_walltime.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_walltime --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_windows_walltime.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions_baseline --profile windows --json profiler_exceptions_baseline_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions_baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_baseline_windows.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions --profile windows --json profiler_exceptions_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_windows.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_cpu --profile windows --json profiler_cpu_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_cpu --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_cpu_windows.json"

elif [ "$1" = "linux" ]; then
    echo "Running Linux  x64 throughput tests"

    rm -f profiler_baseline_linux.json
    rm -f profiler_linux.json
    rm -f profiler_exceptions_baseline_linux.json
    rm -f profiler_exceptions_linux.json
    rm -f profiler_cpu_linux.json
    rm -f profiler_cpu_timer_create_linux.json

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_baseline --profile linux --json profiler_baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_baseline_linux.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler --profile linux --json profiler_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_linux.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions_baseline --profile linux --json profiler_exceptions_baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions_baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_baseline_linux.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_exceptions --profile linux --json profiler_exceptions_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_exceptions --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_exceptions_linux.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario profiler_cpu_timer_create --profile linux --json profiler_cpu_timer_create_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=profiler_cpu --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="profiler_cpu_timer_create_linux.json"
else
    echo "Unknown argument $1"
    exit 1
fi
