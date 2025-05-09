@echo off
setlocal enabledelayedexpansion

rem --- 1. Setup ---
set "DOTNETSDK_VERSION=9.0.100"

rem --- 2. Dynamic Directories ---
set "SCRIPT_DIR=%~dp0"
if "%SCRIPT_DIR:~-1%"=="\" set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
for %%I in ("%SCRIPT_DIR%\..\..") do set "ROOT_DIR=%%~fI"
set "BUILD_DIR=%ROOT_DIR%\tracer\build\_build"

rem --- 3. Docker Image & Platform ---
set "IMAGE_NAME=tonyredondo504/dd-trace-dotnet_alpine-base-debug-%DOTNETSDK_VERSION%"
set "DOCKER_DEFAULT_PLATFORM=linux/arm64"

rem --- 4. Pull or Build Image ---
echo Pulling image %IMAGE_NAME%...
docker pull %IMAGE_NAME%
if %ERRORLEVEL% NEQ 0 (
  echo Image %IMAGE_NAME% not found on registry. Building locally...
  docker build ^
    --build-arg DOTNETSDK_VERSION=%DOTNETSDK_VERSION% ^
    --tag %IMAGE_NAME% ^
    --file "%BUILD_DIR%\docker\alpine_debug.dockerfile" ^
    "%BUILD_DIR%"
)

rem --- 5. Run Container ---
docker run -it --rm ^
  --mount type=bind,source="%ROOT_DIR%",target=/project ^
  --env NugetPackageDirectory=/project/packages ^
  --env artifacts=/project/tracer/bin/artifacts ^
  --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 ^
  --env NUKE_TELEMETRY_OPTOUT=1 ^
  -p 5003:5003 ^
  -v /var/log/datadog:/var/log/datadog/dotnet ^
  %IMAGE_NAME% ^
  /bin/bash
