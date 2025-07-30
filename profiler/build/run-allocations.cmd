@echo on

call install_timeit.cmd

call run_timeit.cmd Allocations.windows.json

exit /b %ERRORLEVEL%