#!/bin/bash
sha="$(git rev-parse HEAD)"
echo "sha=$sha"
echo "SYSTEM_PULLREQUEST_SOURCECOMMITID=$SYSTEM_PULLREQUEST_SOURCECOMMITID"
echo "BUILD_SOURCEVERSION=$BUILD_SOURCEVERSION"
echo "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI=$SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
echo "BUILD_REPOSITORY_URI=$BUILD_REPOSITORY_URI"

repository="--application.source.repository $SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI"
commit="--application.source.branchOrCommit #$SYSTEM_PULLREQUEST_SOURCECOMMITID"

crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile windows --output baseline_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=windows --property arch=x64
dd-trace --crank-import="baseline_windows.json"
rm baseline_windows.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario callsite --profile windows --output callsite_windows.json $repository $commit --property name=AspNetCoreSimpleController --property scenario=callsite --property profile=windows --property arch=x64
dd-trace --crank-import="callsite_windows.json"
rm callsite_windows.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile windows --output calltarget_windows.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=windows --property arch=x64
dd-trace --crank-import="calltarget_windows.json"
rm calltarget_windows.json


crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux --output baseline_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=baseline --property profile=linux --property arch=x64
dd-trace --crank-import="baseline_linux.json"
rm baseline_linux.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario callsite --profile linux --output callsite_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=callsite --property profile=linux --property arch=x64
dd-trace --crank-import="callsite_linux.json"
rm callsite_linux.json

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile linux --output calltarget_linux.json $repository $commit  --property name=AspNetCoreSimpleController --property scenario=calltarget --property profile=linux --property arch=x64
dd-trace --crank-import="calltarget_linux.json"
rm calltarget_linux.json
