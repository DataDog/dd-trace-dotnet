using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Spectre.Console;

namespace StartupBenchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        ProcessArgs(args, out var entryAssemblyPath, out var tracerHomeDirectory, out var tracingLibraryPath);

        var globalEnvVars = new Dictionary<string, string>
        {
            ["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}",
            ["CORECLR_PROFILER_PATH"] = tracingLibraryPath,
            ["DD_DOTNET_TRACER_HOME"] = tracerHomeDirectory
        };

        var runner = new ProcessBenchmarkRunner("dotnet", ["exec", entryAssemblyPath], globalEnvVars);
        var results = runner.RunAll<StartupBenchmarks>();

        var table = new Table();
        table.AddColumn("Name", c => c.LeftAligned());
        table.AddColumn("Time to Main()", c => c.RightAligned());
        table.AddColumn("Time to Exit", c => c.RightAligned());

        AnsiConsole.Live(table)
                   .Start(ctx =>
                   {
                       ctx.Refresh();
                       BenchmarkResults? baseline = null;
                       List<BenchmarkResults> bufferedResults = new();

                       foreach (var result in results)
                       {
                           table.AddRow(
                               result.Name,
                               $"{result.ElapsedToMain.TotalMilliseconds:N0} ms",
                               $"{result.ElapsedToExit.TotalMilliseconds:N0} ms");

                           ctx.Refresh();
                       }
                   });
    }

    private static void ProcessArgs(string[] args, out string entryAssemblyPath, out string tracerHomeDirectory, out string tracingLibraryPath)
    {
        if (args.Length == 2)
        {
            entryAssemblyPath = args[0];
            tracerHomeDirectory = args[1];
        }
        else
        {
            // Console.WriteLine("Usage: dotnet run -- entryAssemblyPath tracerHomeDirectory");
            // return 1;
            entryAssemblyPath = @"D:\source\datadog\dd-trace-dotnet\tracer\test\benchmarks\Startup\EmptyConsoleApp\publish\default\EmptyConsoleApp.dll";
            tracerHomeDirectory = @"C:\Users\Lucas.Pimentel\Downloads\tracer-home-3.9.1";
        }

        if (!File.Exists(entryAssemblyPath))
        {
            throw new FileNotFoundException($"Entry assembly file not found: {entryAssemblyPath}", entryAssemblyPath);
        }

        if (!Directory.Exists(tracerHomeDirectory))
        {
            throw new DirectoryNotFoundException($"Tracer home directory not found: {tracerHomeDirectory}");
        }

        var os = RuntimeInformationHelper.OSPlatform;
        var arch = RuntimeInformationHelper.ProcessArchitecture;

        var extension = os switch
        {
            "win" => "dll",
            "linux" => "so",
            _ => throw new PlatformNotSupportedException($"Platform not supported: {os}-{arch}")
        };

        tracingLibraryPath = $"{tracerHomeDirectory}/win-{arch}/Datadog.Trace.ClrProfiler.Native.{extension}";

        if (!File.Exists(tracingLibraryPath))
        {
            throw new FileNotFoundException($"Tracing library file not found: {tracingLibraryPath}", tracingLibraryPath);
        }
    }
}
