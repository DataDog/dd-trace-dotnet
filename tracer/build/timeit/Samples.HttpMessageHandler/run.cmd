@echo off
IF EXIST results_Samples.HttpMessageHandler.windows.net462.json DEL /F results_Samples.HttpMessageHandler.windows.net462.json
IF EXIST results_Samples.HttpMessageHandler.windows.netcoreapp31.json DEL /F results_Samples.HttpMessageHandler.windows.netcoreapp31.json
IF EXIST results_Samples.HttpMessageHandler.windows.net60.json DEL /F results_Samples.HttpMessageHandler.windows.net60.json

echo *********************
echo .NET Framework 4.6.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.net462.json

echo *********************
echo .NET Core 3.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.netcoreapp31.json

echo *********************
echo .NET Core 6.0
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.net60.json
