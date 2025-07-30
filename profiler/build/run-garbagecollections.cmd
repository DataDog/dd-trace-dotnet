@echo on

call install_timeit.cmd

call run_timeit.cmd GarbageCollections.windows.json

exit /b %ERRORLEVEL%