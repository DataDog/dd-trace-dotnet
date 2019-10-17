#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

cd "$DIR/.."

PUBLISH_OUTPUT="$( pwd )/src/bin/managed-publish"
mkdir -p "$PUBLISH_OUTPUT/netstandard2.0"

dotnet build -c $buildConfiguration src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj

for proj in Datadog.Trace Datadog.Trace.OpenTracing ; do
    dotnet publish -f netstandard2.0 -c $buildConfiguration src/$proj/$proj.csproj
done

dotnet publish -f netstandard2.0 -c $buildConfiguration src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj -o "$PUBLISH_OUTPUT/netstandard2.0"

# Only build Samples.AspNetCoreMvc2 for netcoreapp2.1
if [ "$publishTargetFramework" == "netcoreapp2.1" ]
then
    dotnet publish -f $publishTargetFramework -c $buildConfiguration samples/Samples.AspNetCoreMvc2/Samples.AspNetCoreMvc2.csproj -p:Configuration=$buildConfiguration -p:ManagedProfilerOutputDirectory="$PUBLISH_OUTPUT"
fi

for sample in Samples.Elasticsearch Samples.Elasticsearch.V5 Samples.ServiceStack.Redis Samples.StackExchange.Redis Samples.SqlServer Samples.MongoDB Samples.HttpMessageHandler Samples.Npgsql Samples.MySql Samples.GraphQL ; do
    dotnet publish -f $publishTargetFramework -c $buildConfiguration samples/$sample/$sample.csproj -p:Configuration=$buildConfiguration -p:ManagedProfilerOutputDirectory="$PUBLISH_OUTPUT"
done

for sample in OrleansCrash DataDogThreadTest HttpMessageHandler.StackOverflowException StackExchange.Redis.StackOverflowException AspNetMvcCorePerformance AssemblyLoad.FileNotFoundException TraceContext.InvalidOperationException ; do
    dotnet publish -f $publishTargetFramework -c $buildConfiguration reproductions/$sample/$sample.csproj -p:Configuration=$buildConfiguration -p:ManagedProfilerOutputDirectory="$PUBLISH_OUTPUT"
done

dotnet msbuild Datadog.Trace.proj -t:RestoreAndBuildSamplesForPackageVersions -p:Configuration=$buildConfiguration -p:ManagedProfilerOutputDirectory="$PUBLISH_OUTPUT" -p:TargetFramework=$publishTargetFramework

for proj in Datadog.Trace.ClrProfiler.IntegrationTests ; do
    dotnet publish -f $publishTargetFramework -c $buildConfiguration test/$proj/$proj.csproj
done
