if not exist c:\mnt\ goto nomntdir
@echo PARAMS %*

call "%VSTUDIO_ROOT%\vc\auxiliary\build\vcvars64.bat"
SET VSToolsPath=%VSTUDIO_ROOT%\MSBuild\Microsoft\Visual Studio\v16.0

cd c:\mnt\
dotnet run --project build/_build/_build.csproj -- Info Clean BuildTracerHome PackageTracerHome ZipSymbols SignDlls SignMsiAndNupkg --Artifacts "build-out\%CI_JOB_ID%"
goto :EOF

:nomntdir
@echo directory not mounted, parameters incorrect
exit /b 1
goto :EOF


