## Set the project you want to test
PROJECT=Log4NetExample
APP_PATH=./bin/Debug/net7.0

## Setup automatic instrumentation
## cf https://www.nuget.org/packages/Datadog.Trace.Bundle#readme-body-tab
## Note that the agent must be configured if you want to receive the traces

## .NET Core
export CORECLR_ENABLE_PROFILING=1
export CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
## Replace this path by the appropriate one depending on your system (cf https://www.nuget.org/packages/Datadog.Trace.Bundle#readme-body-tab)
export CORECLR_PROFILER_PATH=$APP_PATH/datadog/osx/Datadog.Trace.ClrProfiler.Native.dylib
export DD_DOTNET_TRACER_HOME=$APP_PATH/datadog

## .NET Framework
# export COR_ENABLE_PROFILING=1
# export COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
## Replace this path by the appropriate one depending on your system (cf https://www.nuget.org/packages/Datadog.Trace.Bundle#readme-body-tab)
# export COR_PROFILER_PATH=$APP_PATH\datadog\win-x64\Datadog.Trace.ClrProfiler.Native.dll
# export DD_DOTNET_TRACER_HOME=<APP_DIRECTORY>/datadog

## Configure logs injection by setting up Unified Service Tagging
## cf https://docs.datadoghq.com/tracing/other_telemetry/connect_logs_and_traces/dotnet/?tab=serilog
export DD_LOGS_INJECTION=true
export DD_ENV=dev
export DD_SERVICE=Log4NetExample
export DD_VERSION=1.0.0

## If you want to enable AgentLess loggingm uncomment those 2 lines and set our api key
## If you are not using agentless logging, the agent must be configured to retrieve your logs
export DD_API_KEY=YOUR_API_KEY
export DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS=Log4Net

## Uncomment if you want to investigate a setup issue
# export DD_TRACE_DEBUG=true

cd $PROJECT
dotnet build
dotnet run