@echo on

call install_timeit.cmd

call run_timeit.cmd GCEventsImpact.windows.json

exit /b %ERRORLEVEL%
