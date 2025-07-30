@echo on

call install_timeit.cmd

call run_timeit.cmd LiveHeap.windows.json

exit /b %ERRORLEVEL%