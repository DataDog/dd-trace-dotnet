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

#windows

crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile windows --json baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="baseline_windows.json"
rm baseline_windows.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile windows --json calltarget_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="calltarget_windows.json"
rm calltarget_windows.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile windows --json calltarget_ngen_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=windows --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="calltarget_ngen_windows.json"
rm calltarget_ngen_windows.json

#linux

crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux --json baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="baseline_linux.json"
rm baseline_linux.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile linux --json calltarget_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="calltarget_linux.json"
rm calltarget_linux.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget_ngen --profile linux --json calltarget_ngen_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget_ngen --property profile=linux --property arch=x64 --variable commit_hash=$commit_sha
dd-trace --crank-import="calltarget_ngen_linux.json"
rm calltarget_ngen_linux.json

#linux arm64

crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux_arm64 --json baseline_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
dd-trace --crank-import="baseline_linux_arm64.json"
rm baseline_linux_arm64.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile linux_arm64 --json calltarget_linux_arm64.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=linux_arm64 --property arch=arm64 --variable commit_hash=$commit_sha
dd-trace --crank-import="calltarget_linux_arm64.json"
rm calltarget_linux_arm64.json

