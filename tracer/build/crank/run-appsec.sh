#!/bin/bash
set -eo pipefail

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

if [ "$1" = "linux" ]; then
    echo "Running Linux  x64 throughput tests"

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux --json baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=appsec_noattack --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="baseline_linux.json"
    rm baseline_linux.json

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile linux --json calltarget_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="calltarget_linux.json"
    rm calltarget_linux.json

    crank --config Security.Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile linux --json calltarget_ngen_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
    dd-trace --crank-import="calltarget_ngen_linux.json"
    rm calltarget_ngen_linux.json

else
    echo "Unknown argument $1"
    exit 1
fi
