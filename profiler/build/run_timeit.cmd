@echo off
setlocal enabledelayedexpansion
set FAILED=0

echo *********************
echo Run x64
echo *********************
dotnet timeit %1 --variable arch=x64

IF ERRORLEVEL 1 set FAILED=1

echo *********************
echo Run x86
echo *********************
dotnet timeit %1 --variable arch=x86

IF ERRORLEVEL 1 set FAILED=1

IF "!FAILED!"=="1" (
    echo One or more benchmarks failed.
    exit /b 1
)
echo All benchmarks completed successfully.
exit /b 0