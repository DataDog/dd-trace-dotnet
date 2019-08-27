#!/bin/bash
set -euxo pipefail

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"

cd "$DIR/.."

dotnet build -c $buildConfiguration src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj

for proj in Datadog.Trace Datadog.Trace.ClrProfiler.Managed Datadog.Trace.OpenTracing ; do
    dotnet publish -f netstandard2.0 -c $buildConfiguration src/$proj/$proj.csproj
done

for sample in Samples.AspNetCoreMvc2 Samples.Elasticsearch Samples.Elasticsearch.V5 Samples.ServiceStack.Redis Samples.StackExchange.Redis Samples.SqlServer Samples.MongoDB Samples.HttpMessageHandler Samples.Npgsql Samples.GraphQL ; do
    dotnet publish -f netcoreapp2.1 -c $buildConfiguration samples/$sample/$sample.csproj
done

for sample in OrleansCrash DataDogThreadTest HttpMessageHandler.StackOverflowException StackExchange.Redis.StackOverflowException AspNetMvcCorePerformance AssemblyLoad.FileNotFoundException TraceContext.InvalidOperationException ; do
    dotnet publish -f netcoreapp2.1 -c $buildConfiguration reproductions/$sample/$sample.csproj
done

dotnet msbuild Datadog.Trace.proj -t:RestoreAndBuildSamplesForPackageVersions -p:Configuration=$buildConfiguration

for proj in Datadog.Trace.ClrProfiler.IntegrationTests ; do
    dotnet publish -f netcoreapp2.1 -c $buildConfiguration test/$proj/$proj.csproj
done
