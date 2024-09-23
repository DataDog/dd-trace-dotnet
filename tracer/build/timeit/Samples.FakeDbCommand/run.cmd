@echo off
IF EXIST results_Samples.FakeDbCommand.windows.net462.json DEL /F results_Samples.FakeDbCommand.windows.net462.json
IF EXIST results_Samples.FakeDbCommand.windows.netcoreapp31.json DEL /F results_Samples.FakeDbCommand.windows.netcoreapp31.json
IF EXIST results_Samples.FakeDbCommand.windows.net60.json DEL /F results_Samples.FakeDbCommand.windows.net60.json
IF EXIST results_Samples.FakeDbCommand.windows.net80.json DEL /F results_Samples.FakeDbCommand.windows.net80.json

echo *********************
echo Installing timeitsharp
echo *********************
dotnet tool update -g timeitsharp --version 0.1.21

echo *********************
echo .NET Framework 4.6.1
echo *********************
dotnet timeit Samples.FakeDbCommand.windows.net462.json

echo *********************
echo .NET Core 3.1
echo *********************
dotnet timeit Samples.FakeDbCommand.windows.netcoreapp31.json

echo *********************
echo .NET Core 6.0
echo *********************
dotnet timeit Samples.FakeDbCommand.windows.net60.json
