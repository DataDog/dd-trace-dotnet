@echo off
setlocal enabledelayedexpansion

REM Clean up old result files if they exist
IF EXIST results_Samples.HttpMessageHandler.windows.net48.json DEL /F results_Samples.HttpMessageHandler.windows.net48.json
IF EXIST results_Samples.HttpMessageHandler.windows.netcoreapp31.json DEL /F results_Samples.HttpMessageHandler.windows.netcoreapp31.json
IF EXIST results_Samples.HttpMessageHandler.windows.net60.json DEL /F results_Samples.HttpMessageHandler.windows.net60.json
IF EXIST results_Samples.HttpMessageHandler.windows.net80.json DEL /F results_Samples.HttpMessageHandler.windows.net80.json

set FAILED=0

echo *********************
echo Installing timeitsharp
echo *********************
dotnet tool update -g timeitsharp --version 0.4.5 --allow-downgrade

echo *********************
echo .NET Framework 4.8
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net48.json
IF ERRORLEVEL 1 (
    echo ❌ .NET Framework 4.8 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 3.1
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.netcoreapp31.json
IF ERRORLEVEL 1 (
    echo ❌ .NET Core 3.1 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 6.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net60.json
IF ERRORLEVEL 1 (
    echo ❌ .NET Core 6.0 benchmark FAILED
    set FAILED=1
)

echo *********************
echo .NET Core 8.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net80.json
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
