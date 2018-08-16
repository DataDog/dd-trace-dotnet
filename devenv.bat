@echo off
rem This batch script sets up the development environment
rem by enabling the Profiler API and starting Visual Studio.
rem Any process started by VS will inherit the environment variables,
rem enabling the profiler for apps run from VS, including while debugging.

rem Enable .NET Framework Profiling API
SET COR_ENABLE_PROFILING=1
SET COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET COR_PROFILER_PATH=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET COR_PROFILER_PATH_64=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET COR_PROFILER_PATH_32=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x86\Debug\Datadog.Trace.ClrProfiler.Native.dll

rem Enable .NET Core Profiling API
SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET CORECLR_PROFILER_PATH=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET CORECLR_PROFILER_PATH_64=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET CORECLR_PROFILER_PATH_32=%~dp0\src\Datadog.Trace.ClrProfiler.Native\x86\Debug\Datadog.Trace.ClrProfiler.Native.dll

rem Limit profiling to these processes only
SET DATADOG_PROFILER_PROCESSES=ConsoleApp.exe;w3wp.exe;iisexpress.exe;Samples.AspNetCoreMvc2.exe;dotnet.exe;Samples.ConsoleFramework.exe
SET DATADOG_INTEGRATIONS=%~dp0\integrations.json

rem Open solution in Visual Studio
IF EXIST "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv.exe" (
START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv.exe" "%~dp0\Datadog.Trace.sln"
) ELSE (
START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\Common7\IDE\devenv.exe" "%~dp0\Datadog.Trace.sln"
)
