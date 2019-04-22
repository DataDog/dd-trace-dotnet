@echo off

SET DD_TRACE_SOLUTION_ROOT=%~dp0

rem Enable .NET Framework Profiling API
SET COR_ENABLE_PROFILING=1
SET COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
rem Needs the path set in the solution based on build

rem Enable .NET Core Profiling API
SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
rem Needs the path set in the solution based on build

rem Limit profiling to these processes only
SET DD_PROFILER_PROCESSES=w3wp.exe;iisexpress.exe;Samples.AspNetCoreMvc2.exe;dotnet.exe;Samples.SqlServer.exe;Samples.RedisCore.exe;Samples.Elasticsearch.exe;Samples.MongoDB.exe;Samples.Npgsql.exe;wcfsvchost.exe

rem Set location of integration definitions
SET DD_INTEGRATIONS=%~dp0\integrations.json;%~dp0\test-integrations.json
