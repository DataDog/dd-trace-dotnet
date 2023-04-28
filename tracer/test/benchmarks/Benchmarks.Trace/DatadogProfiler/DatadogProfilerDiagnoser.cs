using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Datadog.Trace;

#nullable enable

namespace Benchmarks.Trace.DatadogProfiler;

/// <summary>
/// Datadog profiler diagnoser
/// </summary>
internal class DatadogProfilerDiagnoser : IDiagnoser
{
    public IEnumerable<string> Ids { get; } = new[] { "DatadogProfiler" };
    public IEnumerable<IExporter> Exporters { get; } = Array.Empty<IExporter>();
    public IEnumerable<IAnalyser> Analysers { get; } = Array.Empty<IAnalyser>();

    public RunMode GetRunMode(BenchmarkCase benchmarkCase)
    {
        return RunMode.NoOverhead;
    }

    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase)
    {
        return false;
    }

    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        if (signal == HostSignal.BeforeProcessStart)
        {
            string? monitorHome, profiler32Path, profiler64Path, ldPreload, loaderConfig;
            if (FrameworkDescription.Instance.IsWindows())
            {
                monitorHome = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..\\..\\..\\..\\..\\..\\..\\shared\\bin\\monitoring-home");
                monitorHome = Path.GetFullPath(monitorHome);
                profiler32Path = Path.Combine(monitorHome, "win-x86\\Datadog.Trace.ClrProfiler.Native.dll");
                profiler64Path = Path.Combine(monitorHome, "win-x64\\Datadog.Trace.ClrProfiler.Native.dll");
                loaderConfig = Path.Combine(monitorHome, "win-x64\\loader.conf");
                ldPreload = null;
            }
            else
            {
                monitorHome = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "../../../../../../../shared/bin/monitoring-home/");
                monitorHome = Path.GetFullPath(monitorHome);
                profiler32Path = null;
                profiler64Path = Path.Combine(monitorHome, "linux-x64/Datadog.Trace.ClrProfiler.Native.so");
                loaderConfig = Path.Combine(monitorHome, "linux-x64/loader.conf");
                ldPreload = Path.Combine(monitorHome, "linux-x64/Datadog.Linux.ApiWrapper.x64.so");
            }

            var environment = parameters.Process.StartInfo.Environment;
            environment["COR_ENABLE_PROFILING"] = "1";
            environment["CORECLR_ENABLE_PROFILING"] = "1";
            environment["COR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
            environment["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
            environment["DD_DOTNET_TRACER_HOME"] = monitorHome;
            
            if (profiler32Path != null)
            {
                environment["COR_PROFILER_PATH_32"] = profiler32Path;
                environment["CORECLR_PROFILER_PATH_32"] = profiler32Path;
            }

            if (profiler64Path != null)
            {
                environment["COR_PROFILER_PATH_64"] = profiler64Path;
                environment["CORECLR_PROFILER_PATH_64"] = profiler64Path;
            }

            if (ldPreload != null)
            {
                environment["LD_PRELOAD"] = ldPreload;
            }

            if (loaderConfig != null)
            {
                environment["DD_NATIVELOADER_CONFIGFILE"] = loaderConfig;
            }

            environment["DD_TRACE_ENABLED"] = "0";
            environment["DD_PROFILING_ENABLED"] = "1";
            environment["DD_PROFILING_WALLTIME_ENABLED"] = "1";
            environment["DD_PROFILING_CPU_ENABLED"] = "1";
            environment["DD_PROFILING_ALLOCATION_ENABLED"] = "1";
        }
    }

    public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
    {
        return Enumerable.Empty<Metric>();
    }

    public void DisplayResults(ILogger logger)
    {
    }

    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
    {
        return Enumerable.Empty<ValidationError>();
    }
}
