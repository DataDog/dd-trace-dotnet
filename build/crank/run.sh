#!/bin/bash

repository="--application.source.repository $BUILD_REPOSITORY_URI"
commit="--application.source.branchOrCommit $BUILD_SOURCEVERSION"
options="--application.options.counterProviders System.Runtime --application.options.counterProviders Microsoft-AspNetCore-Server-Kestrel --application.options.counterProviders Microsoft.AspNetCore.Hosting --application.options.counterProviders System.Net.Http --application.options.counterProviders Microsoft.AspNetCore.Http.Connections --output results.json --application.options.displayOutput true"

crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile windows $repository $commit $options
dd-trace --crank-import="results.json"

crank --config Samples.AspNetCoreSimpleController.yml --scenario callsite --profile windows $repository $commit $options
dd-trace --crank-import="results.json"

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile windows $repository $commit $options
dd-trace --crank-import="results.json"


crank --config Samples.AspNetCoreSimpleController.yml --scenario baseline --profile linux $repository $commit $options
dd-trace --crank-import="results.json"

crank --config Samples.AspNetCoreSimpleController.yml --scenario callsite --profile linux $repository $commit $options
dd-trace --crank-import="results.json"

crank --config Samples.AspNetCoreSimpleController.yml --scenario calltarget --profile linux $repository $commit $options
dd-trace --crank-import="results.json"