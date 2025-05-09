@echo off
REM ---------------------------------------------------------------
REM Exit immediately if a command fails
REM (CMD doesn’t have a direct equivalent of bash -e; we check errorlevel manually)
REM ---------------------------------------------------------------

REM Change to the directory where this script resides:
cd /d "%~dp0"
set "SCRIPT_DIR=%CD%"

REM Go up one level to get the root directory:
cd ..
set "ROOT_DIR=%CD%"

REM Define build directory and Docker image name:
set "BUILD_DIR=%ROOT_DIR%\tracer\build\_build"
set "IMAGE_NAME=dd-trace-dotnet/alpine-base-debug"

REM ---------------------------------------------------------------
REM Build the Docker image for linux/arm64
REM ---------------------------------------------------------------
set "DOCKER_DEFAULT_PLATFORM=linux/arm64"
docker build ^
  --build-arg DOTNETSDK_VERSION=9.0.100 ^
  --tag %IMAGE_NAME% ^
  --file "%BUILD_DIR%\docker\alpine_debug.dockerfile" ^
  "%BUILD_DIR%"
if errorlevel 1 (
  echo ERROR: Docker build failed.
  exit /b 1
)
REM Clear the override so it doesn’t leak into your environment
set "DOCKER_DEFAULT_PLATFORM="

REM ---------------------------------------------------------------
REM Run the container interactively, mounting your project and logs
REM ---------------------------------------------------------------
set "DOCKER_DEFAULT_PLATFORM=linux/arm64"
docker run -it --rm ^
  --mount type=bind,source="%ROOT_DIR%",target=/project ^
  --env NugetPackageDirectory=/project/packages ^
  --env artifacts=/project/tracer/bin/artifacts ^
  --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 ^
  --env NUKE_TELEMETRY_OPTOUT=1 ^
  -p 5003:5003 ^
  -v "%ROOT_DIR%\var\log\datadog":"/var/log/datadog/dotnet" ^
  %IMAGE_NAME% ^
  /bin/bash
if errorlevel 1 (
  echo ERROR: Docker run failed.
  exit /b 1
)
REM Clear the override again
set "DOCKER_DEFAULT_PLATFORM="

endlocal
