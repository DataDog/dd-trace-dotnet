#!/bin/bash

if [ -z "$1" ]
then
	echo "Expected a commpand line parameter specifying the DD-Prof-DotNet Alpha Deploy Directory. Using default."
	export DD_DOTNET_PROFILER_HOME=`dirname $(realpath $BASH_SOURCE)`
else
    echo "Using the specified command line parameter to configure the DD-Prof-DotNet Alpha Deploy Directory."
	export DD_DOTNET_PROFILER_HOME=$1
fi


export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER={BD1A650D-AC5D-4896-B64F-D6FA25D6B26A}
export CORECLR_PROFILER_PATH_64=${DD_DOTNET_PROFILER_HOME}/linux-x64/Datadog.Profiler.Native.so
export CORECLR_PROFILER_PATH_32=${DD_DOTNET_PROFILER_HOME}/linux-x86/Datadog.Profiler.Native.so

export COMPlus_EnableDiagnostics=1
export DD_PROFILING_ENABLED=1

# We must specify to ld command where to look for our native profiler library.
# This is needed by the managed profiler for its P/Invoke part.
export LD_LIBRARY_PATH=${DD_DOTNET_PROFILER_HOME}
export LD_PRELOAD=${DD_DOTNET_PROFILER_HOME}/linux-x64/Datadog.Linux.ApiWrapper.x64.so

echo "DD_DOTNET_PROFILER_HOME=${DD_DOTNET_PROFILER_HOME}"
