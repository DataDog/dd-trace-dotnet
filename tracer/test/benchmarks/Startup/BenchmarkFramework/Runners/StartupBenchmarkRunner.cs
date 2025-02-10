using System.Collections.ObjectModel;
using System.Diagnostics;

namespace BenchmarkFramework.Runners;

public class StartupBenchmarkRunner<TBenchmarkContainer>
    : BenchmarkRunner<TBenchmarkContainer, ProcessStartInfo>
    where TBenchmarkContainer : new()
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

    protected override double RunOnce(Action<ProcessStartInfo> benchmarkAction)
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

        benchmarkAction(startInfo);

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
        // var elapsedToExit = stopwatch.Elapsed.TotalMilliseconds;
        return elapsedToMain;
    }
}
