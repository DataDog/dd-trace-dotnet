@echo on

call install_timeit.cmd

call run_timeit.cmd Contention.windows.json

exit /b %ERRORLEVEL%