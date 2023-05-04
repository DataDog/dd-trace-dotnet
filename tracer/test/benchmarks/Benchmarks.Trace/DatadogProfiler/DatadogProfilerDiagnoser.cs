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
using Datadog.Trace.Ci;
using Datadog.Trace.Util;

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
            var monitoringHome = EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
            
            // if the monitoringHome is defined but invalid, we clean the monitoringHome variable.
            if (monitoringHome is not null && !Directory.Exists(monitoringHome))
            {
                monitoringHome = null;
            }

            string? profiler32Path, profiler64Path, ldPreload, loaderConfig;

            // if monitoring home is not defined, then we try to locate it in the default path using relative path from the benchmark assembly.
            monitoringHome ??= Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "..", "..", "..", "..", "..", "..", "..", "shared", "bin", "monitoring-home");

            // clean the path
            monitoringHome = Path.GetFullPath(monitoringHome);

            if (FrameworkDescription.Instance.IsWindows())
            {
                // set required file paths
                profiler32Path = Path.Combine(monitoringHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");
                profiler64Path = Path.Combine(monitoringHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
                loaderConfig = Path.Combine(monitoringHome, "win-x64", "loader.conf");
                ldPreload = null;
            }
            else
            {
                // set required file paths
                profiler32Path = null;
                profiler64Path = Path.Combine(monitoringHome, "linux-x64", "Datadog.Trace.ClrProfiler.Native.so");
                loaderConfig = Path.Combine(monitoringHome, "linux-x64", "loader.conf");
                ldPreload = Path.Combine(monitoringHome, "linux-x64", "Datadog.Linux.ApiWrapper.x64.so");
            }

            if (!File.Exists(profiler64Path))
            {
                throw new FileNotFoundException(null, profiler64Path);
            }

            if (profiler32Path is not null && !File.Exists(profiler64Path))
            {
                throw new FileNotFoundException(null, profiler32Path);
            }
            
            if (!File.Exists(loaderConfig))
            {
                throw new FileNotFoundException(null, loaderConfig);
            }

            var environment = parameters.Process.StartInfo.Environment;
            if (!environment.TryGetValue("DD_SERVICE", out _))
            {
                environment["DD_SERVICE"] = parameters.BenchmarkCase.Descriptor.FolderInfo;
            }

            if (!environment.TryGetValue("DD_ENV", out _))
            {
                environment["DD_ENV"] = "benchmarks";
            }

            if (!environment.TryGetValue("DD_VERSION", out _))
            {
                environment["DD_VERSION"] = CIEnvironmentValues.Instance.Branch;
            }

            environment["COR_ENABLE_PROFILING"] = "1";
            environment["CORECLR_ENABLE_PROFILING"] = "1";
            environment["COR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
            environment["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
            environment["DD_DOTNET_TRACER_HOME"] = monitoringHome;
            
            if (profiler32Path != null)
            {
                environment["COR_PROFILER_PATH_32"] = profiler32Path;
                environment["CORECLR_PROFILER_PATH_32"] = profiler32Path;
            }

            environment["COR_PROFILER_PATH_64"] = profiler64Path;
            environment["CORECLR_PROFILER_PATH_64"] = profiler64Path;

            if (ldPreload != null)
            {
                environment["LD_PRELOAD"] = ldPreload;
            }

            environment["DD_NATIVELOADER_CONFIGFILE"] = loaderConfig;

            environment["DD_TRACE_ENABLED"] = "0";
            environment["DD_PROFILING_ENABLED"] = "1";
            environment["DD_PROFILING_WALLTIME_ENABLED"] = "1";
            environment["DD_PROFILING_CPU_ENABLED"] = "1";
            environment["DD_PROFILING_ALLOCATION_ENABLED"] = "1";
            environment["DD_PROFILING_CONTENTION_ENABLED"] = "1";
            environment["DD_PROFILING_EXCEPTION_ENABLED"] = "1";
            environment["DD_PROFILING_HEAP_ENABLED"] = "1";
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
