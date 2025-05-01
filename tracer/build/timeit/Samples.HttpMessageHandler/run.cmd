@echo off
IF EXIST results_Samples.HttpMessageHandler.windows.netfx.json DEL /F results_Samples.HttpMessageHandler.windows.netfx.json
IF EXIST results_Samples.HttpMessageHandler.windows.netcoreapp31.json DEL /F results_Samples.HttpMessageHandler.windows.netcoreapp31.json
IF EXIST results_Samples.HttpMessageHandler.windows.net60.json DEL /F results_Samples.HttpMessageHandler.windows.net60.json
IF EXIST results_Samples.HttpMessageHandler.windows.net80.json DEL /F results_Samples.HttpMessageHandler.windows.net80.json

echo *********************
echo Installing timeitsharp
echo *********************
dotnet tool update -g timeitsharp --version 0.3.2

echo *********************
echo .NET Framework 4.6.1
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.netfx.json

echo *********************
echo .NET Core 3.1
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.netcoreapp31.json

echo *********************
echo .NET Core 6.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net60.json

echo *********************
echo .NET Core 8.0
echo *********************
dotnet timeit Samples.HttpMessageHandler.windows.net80.json
