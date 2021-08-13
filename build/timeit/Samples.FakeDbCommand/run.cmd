@echo off
echo *********************
echo .NET Framework 4.6.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.FakeDbCommand.windows.net461.json

echo *********************
echo .NET Core 3.1
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.FakeDbCommand.windows.netcoreapp31.json

echo *********************
echo .NET Core 5.0
echo *********************
%GOPATH%\\bin\\timeit.exe Samples.FakeDbCommand.windows.net50.json
