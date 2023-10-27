@echo off

@echo =======================================================
@echo          ____        __        ____
@echo         / __ \____ _/ /_____ _/ __ \____  ____ _
@echo        / / / / __ `/ __/ __ `/ / / / __ \/ __ `/
@echo       / /_/ / /_/ / /_/ /_/ / /_/ / /_/ / /_/ /
@echo      /_____/\__,_/\__/\__,_/_____/\____/\__, /
@echo                                        /____/
@echo =======================================================
@echo.
@echo Agent Windows Build Docker Container

@echo AWS_NETWORKING is %AWS_NETWORKING%
if defined AWS_NETWORKING (
    @echo Detected AWS container, setting up networking
    powershell -C "c:\aws_networking.ps1"
)

if not exist c:\mnt\ goto nomntdir
@echo PARAMS %*

call "%VSTUDIO_ROOT%\vc\auxiliary\build\vcvars64.bat"
SET VSToolsPath=%VSTUDIO_ROOT%\MSBuild\Microsoft\VisualStudio\v17.0

cd c:\mnt\

mklink /d dd-trace-dotnet C:\mnt
mklink /d _build C:\_build

dotnet run --project tracer/build/_build/_build.csproj -- Info Clean BuildTracerHome BuildProfilerHome BuildNativeLoader BuildDdDotnet PackageTracerHome ZipSymbols SignDlls SignMsi --Artifacts "build-out\%CI_JOB_ID%"

IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%

goto :EOF

:nomntdir
@echo directory not mounted, parameters incorrect
exit /b 1
goto :EOF


