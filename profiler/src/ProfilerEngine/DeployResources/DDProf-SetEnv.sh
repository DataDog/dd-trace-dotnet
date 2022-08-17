#!/bin/bash

echo "Should be run from <github tracer root>/shared/bin/monitoring-home"

if [ -z "$1" ]
then
    echo "Expected a command line parameter specifying the DD-Prof-DotNet Deploy Directory. Using where the script is by default."
    export DD_DEPLOY_DIRECTORY=`dirname $(realpath $BASH_SOURCE)`
else
    echo "Using the specified command line parameter to configure the DD-Prof-DotNet Deploy Directory."
    export DD_DEPLOY_DIRECTORY=$1
fi


export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}
export CORECLR_PROFILER_PATH_64=${DD_DEPLOY_DIRECTORY}/Datadog.Profiler.Native.so

export COMPlus_EnableDiagnostics=1
export DD_PROFILING_ENABLED=1

export LD_PRELOAD=${DD_DEPLOY_DIRECTORY}/Datadog.Linux.ApiWrapper.x64.so

echo "DD_DEPLOY_DIRECTORY=${DD_DEPLOY_DIRECTORY}"
