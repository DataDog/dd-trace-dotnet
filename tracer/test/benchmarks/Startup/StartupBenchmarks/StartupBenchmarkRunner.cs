using System.Collections.ObjectModel;
using System.Diagnostics;

namespace StartupBenchmarks;

public class StartupBenchmarkRunner
    : BenchmarkRunner<StartupBenchmarks, ProcessStartInfo>
{
    private readonly string _fileName;
    private readonly Collection<string> _arguments;
    private readonly Dictionary<string, string> _globalEnvVars;

    public StartupBenchmarkRunner(
        string fileName,
        IList<string> arguments,
        Dictionary<string, string> globalEnvVars)
    {
        _fileName = fileName;
        _arguments = new Collection<string>(arguments);
        _globalEnvVars = globalEnvVars;
    }

    protected override BenchmarkResults RunOnce(Benchmark<ProcessStartInfo> benchmark)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _fileName,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var envVar in _globalEnvVars)
        {
            startInfo.Environment[envVar.Key] = envVar.Value;
        }

        benchmark.Action(startInfo);

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)!;

        var elapsedToMain = 0.0;

        while (process.StandardOutput.ReadLine() is { } output)
        {
            if (output.Contains("STARTED"))
            {
                elapsedToMain = stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();
                break;
            }
        }

        process.WaitForExit();
        var elapsedToExit = stopwatch.Elapsed.TotalMilliseconds;

        return new BenchmarkResults(
            benchmark.Order,
            benchmark.Name,
            benchmark.IsBaseline,
            [elapsedToMain, elapsedToExit],
            RemovedOutliers: []);
    }
}
