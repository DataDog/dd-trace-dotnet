@echo off
rem This batch script sets up the development environment
rem by enabling the Profiler API and starting Visual Studio.
rem Any process started by VS will inherit the environment variables,
rem enabling the profiler for apps run from VS, including while debugging.

rem Enable .NET Framework Profiling API
SET COR_ENABLE_PROFILING=1
SET COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET COR_PROFILER_PATH=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET COR_PROFILER_PATH_64=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET COR_PROFILER_PATH_32=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x86\Debug\Datadog.Trace.ClrProfiler.Native.dll

rem Enable .NET Core Profiling API
SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET CORECLR_PROFILER_PATH=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET CORECLR_PROFILER_PATH_64=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll
SET CORECLR_PROFILER_PATH_32=%USERPROFILE%\source\repos\dd-trace-csharp\src\Datadog.Trace.ClrProfiler.Native\x86\Debug\Datadog.Trace.ClrProfiler.Native.dll

rem Limit profiling to these processes only
SET DATADOG_PROFILER_PROCESSES=ConsoleApp.exe;w3wp.exe;iisexpress.exe;AspNetCoreMvc2.exe;dotnet.exe

rem Open solution in Visual Studio
START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv.exe" "%UserProfile%\source\repos\dd-trace-csharp\Datadog.Trace.sln"
