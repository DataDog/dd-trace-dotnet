@echo off
rem This batch script sets up the development environment
rem by enabling the Profiler API and starting Visual Studio.
rem Any process started by VS will inherit the environment variables,
rem enabling the profiler for apps run from VS, including while debugging.

rem Set default values
set profiler_platform=x64
set profiler_configuration=Debug
set start_visual_studio=true
set vs_sln_name=Datadog.Trace.sln

:next_argument
set devenv_arg1=%1

if not "%devenv_arg1%" == "" (
    if /i "%devenv_arg1%" == "Debug" (
        set profiler_configuration=Debug
    ) else if /i "%devenv_arg1%" == "Release" (
        set profiler_configuration=Release
    ) else if /i "%devenv_arg1%" == "x64" (
        set profiler_platform=x64
    ) else if /i "%devenv_arg1%" == "x86" (
        set profiler_platform=x86
    ) else if /i "%devenv_arg1%" == "vs+" (
        set start_visual_studio=true
    ) else if /i "%devenv_arg1%" == "vs-" (
        set start_visual_studio=false
    ) else (
        echo Invalid option: "%devenv_arg1%".
        goto show_usage
    )
    
    shift
    goto next_argument
)

echo Enabling profiler for "%profiler_configuration%/%profiler_platform%".

rem Enable .NET Framework Profiling API
SET COR_ENABLE_PROFILING=1
SET COR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET COR_PROFILER_PATH=%~dp0\src\Datadog.Trace.ClrProfiler.Native\bin\%profiler_configuration%\%profiler_platform%\Datadog.Trace.ClrProfiler.Native.dll

rem Enable .NET Core Profiling API
SET CORECLR_ENABLE_PROFILING=1
SET CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
SET CORECLR_PROFILER_PATH=%~dp0\src\Datadog.Trace.ClrProfiler.Native\bin\%profiler_configuration%\%profiler_platform%\Datadog.Trace.ClrProfiler.Native.dll

rem Limit profiling to these processes only
SET DD_PROFILER_EXCLUDE_PROCESSES=devenv.exe;Microsoft.ServiceHub.Controller.exe;ServiceHub.Host.CLR.exe

rem Set location of integration definitions
SET DD_INTEGRATIONS=%~dp0\integrations.json;%~dp0\test-integrations.json

if "%start_visual_studio%" == "true" (
    echo Starting Visual Studio...

    IF EXIST "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\Common7\IDE\devenv.exe" (
        START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\Common7\IDE\devenv.exe" "%~dp0\%vs_sln_name%"
    ) ELSE IF EXIST "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.exe" (
        START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\Common7\IDE\devenv.exe" "%~dp0\%vs_sln_name%"
    ) ELSE IF EXIST "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv.exe" (
        START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Professional\Common7\IDE\devenv.exe" "%~dp0\%vs_sln_name%"
    ) ELSE (
        START "Visual Studio" "%ProgramFiles(x86)%\Microsoft Visual Studio\2017\Community\Common7\IDE\devenv.exe" "%~dp0\%vs_sln_name%"
    )
)
goto end

:show_usage
echo Usage: %0 [Release^|Debug] [x64^|x86] [vs+^|vs-]
echo   All arguments are optional and can be provided in any order.
echo   If an argument is provided multiple times, the last value wins.
echo   The default configuration is "Release".
echo   The default platform is "x64".
echo   Visual Studio is started unless "vs-" is specified.

:end
rem Clear temporary values
set profiler_platform=
set profiler_configuration=
set start_visual_studio=
set vs_sln_name=
set devenv_arg1=
set devenv_arg1_sub7=