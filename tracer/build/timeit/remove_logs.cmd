@echo off
REM Recursively walk through all files under the Datadog .NET Tracer logs folder
for /R "%ProgramData%\Datadog .NET Tracer\logs" %%F in (*.*) do (

    REM Check if the filename (%%~nxF = name + extension) contains "timeit"
    REM The `find /I` command searches case-insensitively
    echo %%~nxF | find /I "timeit" >nul

    REM If "timeit" is NOT found (errorlevel=1), delete the file
    if errorlevel 1 del /Q "%%F"
)
