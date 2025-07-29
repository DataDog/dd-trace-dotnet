@echo on

call install_timeit.cmd

call run_timeit.cmd CpuWallTime.windows.json

exit /b %ERRORLEVEL%