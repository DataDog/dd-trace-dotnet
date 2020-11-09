if not exist c:\mnt\ goto nomntdir
@echo PARAMS %*

REM don't use `OUTDIR` as an environment variable. It will confuse the VC build
set PKG_OUTDIR=c:\mnt\build-out\%CI_JOB_ID%
call "%VSTUDIO_ROOT%\vc\auxiliary\build\vcvars64.bat"

mkdir \build
cd \build || exit /b 2
xcopy /e/s/h/q c:\mnt\*.* || exit /b 3

pip install invoke
inv -e dotnettracer.build

if not exist %PKG_OUTDIR% mkdir %PKG_OUTDIR% || exit /b 6
xcopy /y/e/s output\*.* %PKG_OUTDIR%
goto :EOF

:nomntdir
@echo directory not mounted, parameters incorrect
exit /b 1
goto :EOF


