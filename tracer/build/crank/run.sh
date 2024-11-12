#!/bin/bash
set -eo pipefail

sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "SYSTEM_PULLREQUEST_SOURCECOMMITID=$SYSTEM_PULLREQUEST_SOURCECOMMITID"
echo "BUILD_SOURCEVERSION=$BUILD_SOURCEVERSION"
echo "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI=$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
echo "BUILD_REPOSITORY_URI=$BUILD_REPOSITORY_URI"
echo "DD_CIVISIBILITY_AGENTLESS_ENABLED=$DD_CIVISIBILITY_AGENTLESS_ENABLED"
echo "Will run extended throughput tests: $2"

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

    echo "Cleaning previous results"
    rm -f baseline_windows.json
    rm -f calltarget_ngen_windows.json
    rm -f trace_stats_windows.json
    rm -f manual_only_windows.json
    rm -f manual_and_automatic_windows.json
    
    echo "Running windows throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile windows --json baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_windows.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile windows --json calltarget_ngen_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="calltarget_ngen_windows.json"

    if [ "$2" = "True" ]; then
      echo "Running throughput tests with stats enabled"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario trace_stats --profile windows --json trace_stats_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=trace_stats --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
      dd-trace --crank-import="trace_stats_windows.json"
      
      echo "Running manual-only instrumentation throughput tests"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_only --profile windows --json manual_only_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_only --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha 
      dd-trace --crank-import="manual_only_windows.json"
      
      echo "Running DD_TRACE_ENABLED=0 throughput tests"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario ddtraceenabled_false --profile windows --json ddtraceenabled_false_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=ddtraceenabled_false --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha 
      dd-trace --crank-import="ddtraceenabled_false_windows.json"

    fi

    echo "Running manual instrumentation throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_and_automatic --profile windows --json manual_and_automatic_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_and_automatic --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="manual_and_automatic_windows.json"

elif [ "$1" = "linux" ]; then
    echo "Cleaning previous results"
    rm -f baseline_linux.json
    rm -f calltarget_ngen_linux.json
    rm -f trace_stats_linux.json
    rm -f manual_only_linux.json
    rm -f manual_and_automatic_linux.json

    echo "Running Linux x64 throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux --json baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_linux.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile linux --json calltarget_ngen_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="calltarget_ngen_linux.json"

    if [ "$2" = "True" ]; then
      echo "Running throughput tests with stats enabled"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario trace_stats --profile linux --json trace_stats_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=trace_stats --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
      dd-trace --crank-import="trace_stats_linux.json"
      
      echo "Running manual-only instrumentation throughput tests"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_only --profile linux --json manual_only_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_only --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
      dd-trace --crank-import="manual_only_linux.json"
      
      echo "Running DD_TRACE_ENABLED=0 throughput tests"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario ddtraceenabled_false --profile linux --json ddtraceenabled_false_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=ddtraceenabled_false --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha 
      dd-trace --crank-import="ddtraceenabled_false_linux.json"

    fi

    echo "Running manual instrumentation throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_and_automatic --profile linux --json manual_and_automatic_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_and_automatic --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="manual_and_automatic_linux.json"

elif [ "$1" = "linux_arm64" ]; then
    echo "Cleaning previous results"
    rm -f baseline_linux_arm64.json
    rm -f calltarget_ngen_linux_arm64.json
    rm -f trace_stats_linux_arm64.json
    rm -f manual_only_linux_arm64.json
    rm -f manual_and_automatic_linux_arm64.json

    echo "Running Linux arm64 throughput tests"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux_arm64 --json baseline_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_linux_arm64.json"

    crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile linux_arm64 --json calltarget_ngen_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="calltarget_ngen_linux_arm64.json"

    if [ "$2" = "True" ]; then
      echo "Running throughput tests with stats enabled"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario trace_stats --profile linux_arm64 --json trace_stats_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=trace_stats --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
      dd-trace --crank-import="trace_stats_linux_arm64.json"
      
      echo "Running manual-only instrumentation throughput tests"
    crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_only --profile linux_arm64 --json manual_only_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_only --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="manual_only_linux_arm64.json"
      
      echo "Running DD_TRACE_ENABLED=0 throughput tests"
      crank --config Samples.AspNetCoreSimpleController.yml --scenario ddtraceenabled_false --profile linux_arm64 --json ddtraceenabled_false_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=ddtraceenabled_false --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha 
      dd-trace --crank-import="ddtraceenabled_false_linux_arm64.json"

    fi

    echo "Running manual instrumentation throughput tests"
    
    crank --config Samples.AspNetCoreSimpleController.yml --scenario manual_and_automatic --profile linux_arm64 --json manual_and_automatic_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=manual_and_automatic --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="manual_and_automatic_linux_arm64.json"

else
    echo "Unknown argument $1"
    exit 1
fi
