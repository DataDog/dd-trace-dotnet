@echo off
echo *********************
echo .NET Framework 4.6.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.net461.json

echo *********************
echo .NET Core 3.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.netcoreapp31.json

echo *********************
echo .NET Core 5.0
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.HttpMessageHandler.windows.net50.json
