@echo off
%GOPATH%\\bin\\timeit.exe PiComputation.windows.%1.json
%GOPATH%\\bin\\timeit.exe Exceptions.windows.%1.json
%GOPATH%\\bin\\timeit.exe Allocations.windows.%1.json