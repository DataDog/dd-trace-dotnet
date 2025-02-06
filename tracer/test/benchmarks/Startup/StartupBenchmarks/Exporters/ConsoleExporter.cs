using Spectre.Console;

namespace StartupBenchmarks.Exporters;

public class ConsoleExporter
{
    public void ExportBenchmarkResults(IEnumerable<IBenchmark> benchmarks, IEnumerable<BenchmarkResults> results)
    {
        var table = new Table();
        List<BenchmarkResults> outliers = [];

        AnsiConsole.Live(table)
                   .Start(ctx =>
                   {
                       table.AddColumn("Name", c => c.LeftAligned());
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
                           table.AddRow(benchmark.IsBaseline ? $"[bold blue]{benchmark.Name}[/]" : benchmark.Name);
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

        foreach (var outlier in outliers)
        {
            var outlierElapsedTimes = outlier.RemovedOutliers.Select(r => FormatMilliseconds(r.ElapsedTimes.Sum()));
            AnsiConsole.MarkupLine($"Outliers detected in [yellow]\"{outlier.Name}\" total:[/] {string.Join(", ", outlierElapsedTimes)}");
        }
    }

    private static string FormatMilliseconds(double milliseconds, bool isBaseline = false)
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
}
