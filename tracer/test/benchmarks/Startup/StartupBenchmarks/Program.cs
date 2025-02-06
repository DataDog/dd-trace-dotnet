using Spectre.Console;

namespace StartupBenchmarks;

internal static class Program
{
    private const int IterationCount = 10;

    public static void Main(string[] args)
    {
        ProcessArgs(args, out var entryAssemblyPath, out var tracerHomeDirectory, out var tracingLibraryPath);

        var globalEnvVars = new Dictionary<string, string>
        {
            ["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}",
            ["CORECLR_PROFILER_PATH"] = tracingLibraryPath,
            ["DD_DOTNET_TRACER_HOME"] = tracerHomeDirectory
        };

        var table = new Table();

        AnsiConsole.Live(table)
                   .Start(ctx =>
                   {
                       table.AddColumn("Name", c => c.LeftAligned());
                       table.AddColumn("Start-up time", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());
                       table.AddColumn("Exit time", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());
                       table.AddColumn("Total", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());

                       var runner = new StartupBenchmarkRunner("dotnet", ["exec", entryAssemblyPath], globalEnvVars);
                       var benchmarks = runner.GetBenchmarks(out var baseline);

                       foreach (var benchmark in benchmarks)
                       {
                           // insert rows with names only for now
                           table.AddRow(benchmark.IsBaseline ? $"[bold blue]{benchmark.Name}[/]" : benchmark.Name);
                       }

                       table.AddEmptyRow();
                       table.AddRow("[bold green]Running benchmarks...[/]");
                       ctx.Refresh();

                       var results = runner.RunAll(IterationCount);
                       BenchmarkResults? baselineResults = null;

                       foreach (var result in results)
                       {
                           if (result.IsBaseline && baseline is null)
                           {
                               baselineResults = result;
                           }

                           var elapsedTime1 = result.ElapsedTimes[0];
                           var elapsedTime2 = result.ElapsedTimes[1];
                           var totalElapsedTime = elapsedTime1 + elapsedTime2;

                           // replace empty rows with the actual results
                           table.RemoveRow(result.Order);

                           table.InsertRow(
                               Math.Min(result.Order, table.Rows.Count),
                               result.IsBaseline ? $"[bold blue]{result.Name}[/]" : result.Name,
                               FormatMilliseconds(elapsedTime1, result.IsBaseline),
                               FormatRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes, index: 0),
                               FormatMilliseconds(elapsedTime2, result.IsBaseline),
                               FormatRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes, index: 1),
                               FormatMilliseconds(totalElapsedTime, result.IsBaseline),
                               FormatTotalRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes)
                           );

                           ctx.Refresh();
                       }

                       table.RemoveRow(table.Rows.Count - 1);
                       table.RemoveRow(table.Rows.Count - 1);
                       ctx.Refresh();
                   });
    }

    private static string FormatMilliseconds(double milliseconds, bool isBaseline)
    {
        return isBaseline ? $"[bold]{milliseconds:#,##0.00} ms[/]" : $"{milliseconds:#,##0.00} ms";
    }

    private static string FormatRatio(double[] current, double[]? baseline, int index)
    {
        var ratio = current[index] / baseline?[index];
        return ratio?.ToString("#,##0.00") ?? string.Empty;
    }

    private static string FormatTotalRatio(double[] current, double[]? baseline)
    {
        var ratio = current.Sum() / baseline?.Sum();
        return ratio?.ToString("#,##0.00") ?? string.Empty;
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

    private static IEnumerable<double> RemoveOutliers(double[] data)
    {
        // Step 1: Calculate the Median
        var median = GetMedian(data);

        // Step 2: Calculate the Median Absolute Deviation (MAD)
        var absoluteDeviations = data.Select(x => Math.Abs(x - median)).ToArray();
        var mad = GetMedian(absoluteDeviations);

        // Step 3: Compute Modified Z-Scores
        var modifiedZScores = data.Select(x => 0.6745 * (x - median) / mad).ToArray();

        // Step 4: Identify and filter out outliers (|Modified Z| > 3.5)
        return data.Where((_, i) => Math.Abs(modifiedZScores[i]) <= 3.5);
    }

    private static double GetMedian(double[] data)
    {
        var sortedData = data.OrderBy(x => x);
        var n = data.Length;

        if (n % 2 == 0)
        {
            // Even number of elements, average the two middle ones
            // return (sortedData[n / 2 - 1] + sortedData[n / 2]) / 2.0;
            return sortedData.Skip(n / 2 - 2).Take(2).Average();
        }

        // Odd number of elements, take the middle one
        // return sortedData[n / 2];
        return sortedData.ElementAt(n / 2);
    }
}
