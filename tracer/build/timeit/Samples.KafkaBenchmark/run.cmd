@echo off
IF EXIST results_Samples.KafkaBenchmark.windows.net48.json DEL /F results_Samples.KafkaBenchmark.windows.net48.json
IF EXIST results_Samples.KafkaBenchmark.windows.netcoreapp31.json DEL /F results_Samples.KafkaBenchmark.windows.netcoreapp31.json
IF EXIST results_Samples.KafkaBenchmark.windows.net60.json DEL /F results_Samples.KafkaBenchmark.windows.net60.json
IF EXIST results_Samples.KafkaBenchmark.windows.net80.json DEL /F results_Samples.KafkaBenchmark.windows.net80.json

echo *********************
echo Installing timeitsharp
echo *********************
dotnet tool update -g timeitsharp --version 0.3.2

echo *********************
echo .NET Framework 4.8
echo *********************
dotnet timeit Samples.KafkaBenchmark.windows.net48.json

echo *********************
echo .NET Core 3.1
echo *********************
dotnet timeit Samples.KafkaBenchmark.windows.netcoreapp31.json

echo *********************
echo .NET 6.0
echo *********************
dotnet timeit Samples.KafkaBenchmark.windows.net60.json

echo *********************
echo .NET 8.0
echo *********************
dotnet timeit Samples.KafkaBenchmark.windows.net80.json
