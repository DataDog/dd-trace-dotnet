@echo on

call install_timeit.cmd

call run_timeit.cmd OutgoingHttpRequests.windows.json

exit /b %ERRORLEVEL%