using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;

namespace StartupBenchmarks;

public class ProcessBenchmarkRunner
{
    private readonly string _fileName;
    private readonly Collection<string> _arguments;
    private readonly Dictionary<string, string> _globalEnvVars;

    public ProcessBenchmarkRunner(string fileName, IList<string> arguments, Dictionary<string, string> globalEnvVars)
    {
        _fileName = fileName;
        _arguments = new Collection<string>(arguments);
        _globalEnvVars = globalEnvVars;
    }

    public IEnumerable<BenchmarkResults> RunAll<TBenchmarks>()
    {
        foreach (var method in typeof(TBenchmarks).GetMethods())
        {
            if (method.GetCustomAttribute<BenchmarkAttribute>() is { } attribute)
            {
                var benchmark = new StartupBenchmarks();

                var name = attribute.Description ?? method.Name;
                var action = (Action<ProcessStartInfo>)method.CreateDelegate(typeof(Action<ProcessStartInfo>), benchmark);

                yield return Run(name, action, attribute.IsBaseline);
            }
        }
    }

    private BenchmarkResults Run(string name, Action<ProcessStartInfo> setup, bool isBaseline)
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

        setup(startInfo);

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)!;

        var elapsedToMain = TimeSpan.Zero;

        while (process.StandardOutput.ReadLine() is { } output)
        {
            if (output.Contains("STARTED"))
            {
                elapsedToMain = stopwatch.Elapsed;
                stopwatch.Restart();
                break;
            }
        }

        process.WaitForExit();
        return new BenchmarkResults(name, isBaseline, elapsedToMain, stopwatch.Elapsed);
    }
}
