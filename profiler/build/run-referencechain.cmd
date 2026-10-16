@echo on

call install_timeit.cmd

call run_timeit.cmd ReferenceChain.windows.json

exit /b %ERRORLEVEL%