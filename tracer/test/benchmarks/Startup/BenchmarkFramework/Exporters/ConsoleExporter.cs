using Spectre.Console;

namespace BenchmarkFramework.Exporters;

public class ConsoleExporter
{
    public void ExportBenchmarkResults(IEnumerable<IBenchmark> benchmarks, IEnumerable<BenchmarkIterationResults> results)
    {
        var table = new Table();
        List<BenchmarkIterationResults> outliers = [];

        AnsiConsole.Live(table)
                   .Start(ctx =>
                   {
                       table.AddColumn("Benchmark", c => c.LeftAligned());
                       table.AddColumn("Start-up time", c => c.RightAligned());
                       table.AddColumn("Diff", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());

                       foreach (var benchmark in benchmarks)
                       {
                           // insert rows with names only for now
                           table.AddRow(benchmark.IsBaseline ? $"[bold]{benchmark.Name}[/]" : benchmark.Name);
                       }

                       table.AddEmptyRow();
                       table.AddRow("[bold green]Running benchmarks...[/]");
                       ctx.Refresh();

                       BenchmarkIterationResults? baselineResults = null;
                       double? averageBaselineElapsedTime = null;
                       var rowCount = 0;

                       foreach (var result in results)
                       {
                           var benchmark = result.Benchmark;

                           if (benchmark.IsBaseline && baselineResults is null)
                           {
                               baselineResults = result;
                               averageBaselineElapsedTime = result.KeptResults.Average();
                           }

                           if (result.RemovedOutliers.Count > 0)
                           {
                               outliers.Add(result);
                           }

                           // replace each empty row with the actual results
                           table.RemoveRow(rowCount);

                           var averageElapsedTime = result.KeptResults.Average();

                           table.InsertRow(
                               rowCount,
                               benchmark.IsBaseline ? $"[bold]{benchmark.Name}[/]" : benchmark.Name,
                               FormatMilliseconds(averageElapsedTime, benchmark.IsBaseline),
                               FormatMillisecondsDiff(averageElapsedTime - averageBaselineElapsedTime, benchmark.IsBaseline),
                               FormatRatio(averageElapsedTime, averageBaselineElapsedTime, benchmark.IsBaseline));

                           rowCount++;
                           ctx.Refresh();
                       }

                       // remove the status update rows
                       table.RemoveRow(table.Rows.Count - 1);
                       table.RemoveRow(table.Rows.Count - 1);
                       ctx.Refresh();
                   });

        foreach (var outlier in outliers)
        {
            var outlierElapsedTimes = outlier.RemovedOutliers.Select(r => FormatMilliseconds(r));
            AnsiConsole.MarkupLine($"Outliers detected in [yellow]\"{outlier.Benchmark.Name}\"[/]: {string.Join(", ", outlierElapsedTimes)}");
        }
    }

    private static string FormatMilliseconds(double milliseconds, bool isBaseline = false)
    {
        if (isBaseline)
        {
            return $"[bold]{milliseconds:#,##0.00} ms[/]";
        }

        return $"{milliseconds:#,##0.00} ms";
    }

    private static string FormatMillisecondsDiff(double? milliseconds, bool isBaseline)
    {
        if (isBaseline)
        {
            return "[bold](baseline)[/]";
        }

        if (milliseconds is null)
        {
            return "-";
        }

        return $"{milliseconds.Value:#,##0.00} ms";
    }

    private static string FormatRatio(double current, double? baseline, bool isBaseline)
    {
        if (isBaseline)
        {
            return "[bold]1.00[/]";
        }

        if (baseline is null)
        {
            return "-";
        }

        return $"{current / baseline:#,##0.00}";
    }
}
