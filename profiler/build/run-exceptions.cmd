@echo on

call install_timeit.cmd

call run_timeit.cmd Exceptions.windows.json

exit /b %ERRORLEVEL%