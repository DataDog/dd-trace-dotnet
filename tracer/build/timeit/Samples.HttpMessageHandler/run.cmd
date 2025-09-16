@echo off
setlocal enabledelayedexpansion

REM Clean up old result files if they exist
IF EXIST results_Samples.HttpMessageHandler.windows.net48.json DEL /F results_Samples.HttpMessageHandler.windows.net48.json
IF EXIST results_Samples.HttpMessageHandler.windows.netcoreapp31.json DEL /F results_Samples.HttpMessageHandler.windows.netcoreapp31.json
IF EXIST results_Samples.HttpMessageHandler.windows.net60.json DEL /F results_Samples.HttpMessageHandler.windows.net60.json
IF EXIST results_Samples.HttpMessageHandler.windows.net80.json DEL /F results_Samples.HttpMessageHandler.windows.net80.json

REM =====================
REM Handle log artifact directory
REM =====================
IF DEFINED LOG_ARTIFACT_DIR (
    echo Cleaning log artifact directory: %LOG_ARTIFACT_DIR%
    IF EXIST "%LOG_ARTIFACT_DIR%" (
        rmdir /S /Q "%LOG_ARTIFACT_DIR%"
    )
    mkdir "%LOG_ARTIFACT_DIR%"
) ELSE (
    echo LOG_ARTIFACT_DIR not set. Skipping log copy setup.
)

REM Helper macro to copy logs if LOG_ARTIFACT_DIR is set
set COPY_LOGS=IF DEFINED LOG_ARTIFACT_DIR xcopy /E /I /Y "%ProgramData%\Datadog .NET Tracer\logs" "%LOG_ARTIFACT_DIR%"

set FAILED=0

echo *********************
echo Installing timeitsharp
echo *********************
dotnet tool update -g timeitsharp --version 0.4.5 --allow-downgrade

echo *********************
echo .NET Framework 4.8
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net48.json
%COPY_LOGS%
IF ERRORLEVEL 1 (
    echo ❌ .NET Framework 4.8 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 3.1
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.netcoreapp31.json
%COPY_LOGS%
IF ERRORLEVEL 1 (
    echo ❌ .NET Core 3.1 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 6.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net60.json
%COPY_LOGS%
IF ERRORLEVEL 1 (
    echo ❌ .NET Core 6.0 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 8.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net80.json
%COPY_LOGS%
IF ERRORLEVEL 1 (
    echo ❌ .NET Core 8.0 benchmark FAILED
    set FAILED=1
)

IF "!FAILED!"=="1" (
    echo =====================
    echo ❌ One or more benchmarks failed.
    echo =====================
    exit /b 1
)

echo =====================
echo ✅ All benchmarks completed successfully.
echo =====================
exit /b 0
