using System;
using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace StartupBenchmarks;

public class Benchmarks
{
    [Benchmark]
    public void NoTracer()
    {
        var process = new Process();
        process.StartInfo.FileName = "dotnet";
        process.StartInfo.Arguments = "run";

        SetEnvironmentVariables(process.StartInfo.Environment);

        process.Start();
        process.WaitForExit();
    }

    private void SetEnvironmentVariables(IDictionary<string, string> startInfo)
    {
        startInfo["CORECLR_PROFILER"] = "none";
    }
}
