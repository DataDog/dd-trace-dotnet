@echo on

call install_timeit.cmd

:: Run x64
dotnet timeit LiveHeap.windows.json --variable arch=x64

:: Run x86
dotnet timeit LiveHeap.windows.json --variable arch=x86

