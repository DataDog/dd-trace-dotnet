#!/bin/bash
sha="$(git rev-parse HEAD)"
echo "running for sha $sha"

repository="--application.source.repository $BUILD_REPOSITORY_URI"
commit="--application.source.branchOrCommit #$sha"

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
