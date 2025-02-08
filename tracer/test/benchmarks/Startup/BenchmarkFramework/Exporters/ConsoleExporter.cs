using Spectre.Console;

namespace BenchmarkFramework.Exporters;

public class ConsoleExporter
{
    public void ExportBenchmarkResults(IEnumerable<IBenchmark> benchmarks, IEnumerable<BenchmarkResults> results)
    {
        var table = new Table();
        List<BenchmarkResults> outliers = [];

        AnsiConsole.Live(table)
                   .Start(ctx =>
                   {
                       table.AddColumn("Benchmark", c => c.LeftAligned());
                       table.AddColumn("Start-up time", c => c.RightAligned());
                       table.AddColumn("Diff", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());
                       table.AddColumn("Exit time", c => c.RightAligned());
                       table.AddColumn("Diff", c => c.RightAligned());
                       table.AddColumn("Ratio", c => c.RightAligned());
                       table.AddColumn("Total", c => c.RightAligned());
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

                       BenchmarkResults? baselineResults = null;

                       foreach (var result in results)
                       {
                           if (result.IsBaseline && baselineResults is null)
                           {
                               baselineResults = result;
                           }

                           if (result.RemovedOutliers.Count > 0)
                           {
                               outliers.Add(result);
                           }

                           var totalElapsedTime = result.ElapsedTimes.Sum();
                           var totalElapsedTimeBaseline = baselineResults?.ElapsedTimes.Sum();

                           // replace each empty row with the actual results
                           table.RemoveRow(result.Order);

                           table.InsertRow(
                               Math.Min(result.Order, table.Rows.Count),
                               result.IsBaseline ? $"[bold]{result.Name}[/]" : result.Name,
                               FormatMilliseconds(result.ElapsedTimes[0], result.IsBaseline),
                               FormatMillisecondsDiff(result.ElapsedTimes[0] - baselineResults?.ElapsedTimes[0], result.IsBaseline),
                               FormatRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes, index: 0, result.IsBaseline),
                               FormatMilliseconds(result.ElapsedTimes[1], result.IsBaseline),
                               FormatMillisecondsDiff(result.ElapsedTimes[1] - baselineResults?.ElapsedTimes[1], result.IsBaseline),
                               FormatRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes, index: 1, result.IsBaseline),
                               FormatMilliseconds(totalElapsedTime, result.IsBaseline),
                               FormatMillisecondsDiff(totalElapsedTime - totalElapsedTimeBaseline, result.IsBaseline),
                               FormatTotalRatio(result.ElapsedTimes, baselineResults?.ElapsedTimes, result.IsBaseline)
                           );

                           ctx.Refresh();
                       }

                       // remove the status update rows
                       table.RemoveRow(table.Rows.Count - 1);
                       table.RemoveRow(table.Rows.Count - 1);
                       ctx.Refresh();
                   });

        foreach (var outlier in outliers)
        {
            var outlierElapsedTimes = outlier.RemovedOutliers.Select(r => FormatMilliseconds(r.ElapsedTimes.Sum()));
            AnsiConsole.MarkupLine($"Outliers detected in [yellow]\"{outlier.Name}\" total:[/] {string.Join(", ", outlierElapsedTimes)}");
        }
    }

    private static string FormatMilliseconds(double? milliseconds, bool isBaseline = false)
    {
        if (milliseconds is null)
        {
            return string.Empty;
        }

        if (isBaseline)
        {
            return $"[bold]{milliseconds.Value:#,##0.00} ms[/]";
        }

        return $"{milliseconds.Value:#,##0.00} ms";
    }

    private static string FormatMillisecondsDiff(double? milliseconds, bool isBaseline = false)
    {
        if (isBaseline || milliseconds is null)
        {
            return string.Empty;
        }

        if (isBaseline)
        {
            return $"[bold]{milliseconds.Value:#,##0.00} ms[/]";
        }

        return $"{milliseconds.Value:#,##0.00} ms";
    }

    private static string FormatRatio(double[] current, double[]? baseline, int index, bool isBaseline)
    {
        if (isBaseline)
        {
            return "[bold]1.00[/]";
        }

        if (baseline is null)
        {
            return string.Empty;
        }

        return $"{current[index] / baseline[index]:#,##0.00}";
    }

    private static string FormatTotalRatio(double[] current, double[]? baseline, bool isBaseline)
    {
        if (isBaseline)
        {
            return "[bold]1.00[/]";
        }

        if (baseline is null)
        {
            return string.Empty;
        }

        return $"{current.Sum() / baseline.Sum():#,##0.00}";
    }
}
