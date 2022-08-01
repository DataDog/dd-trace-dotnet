if not exist c:\mnt\ goto nomntdir
@echo PARAMS %*

call "%VSTUDIO_ROOT%\vc\auxiliary\build\vcvars64.bat"
SET VSToolsPath=%VSTUDIO_ROOT%\MSBuild\Microsoft\Visual Studio\v16.0

cd c:\mnt\

REM Temporarily fixes to allow building the profiler code from the dd-continuous-profiler-dotnet repo
mklink /d dd-trace-dotnet C:\mnt
mklink /d _build C:\_build

dotnet run --project tracer/build/_build/_build.csproj -- Info Clean BuildTracerHome BuildProfilerHome BuildNativeLoader PackageTracerHome ZipSymbols SignDlls SignMsiAndNupkg --Artifacts "build-out\%CI_JOB_ID%"

IF %ERRORLEVEL% NEQ 0 EXIT /B %ERRORLEVEL%

goto :EOF

:nomntdir
@echo directory not mounted, parameters incorrect
exit /b 1
goto :EOF


