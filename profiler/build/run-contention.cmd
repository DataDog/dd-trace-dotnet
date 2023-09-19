@echo on

call install_timeit.cmd

:: remove DOTNET_ROOT environment variable to ensure we can run
:: the benchmark in x64 and x86
set DOTNET_ROOT=

:: Run x64
dotnet timeit Contention.windows.json --variable arch=x64

:: Run x86
dotnet timeit Contention.windows.json --variable arch=x86

