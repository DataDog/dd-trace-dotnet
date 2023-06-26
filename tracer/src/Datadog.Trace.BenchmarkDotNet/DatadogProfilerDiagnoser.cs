// <copyright file="DatadogProfilerDiagnoser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog BenchmarkDotNet Profiler diagnoser
/// </summary>
internal class DatadogProfilerDiagnoser : IDiagnoser
{
    /// <summary>
    /// Default DatadogExporter instance
    /// </summary>
    public static readonly DatadogProfilerDiagnoser Default = new();

    public Dictionary<int, (TraceId TraceId, ulong SpanId)> IdsByBenchmarks { get; } = new();

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
            var traceId = RandomIdGenerator.Shared.NextTraceId(true);
            var spanId = RandomIdGenerator.Shared.NextSpanId(true);

            IdsByBenchmarks[parameters.BenchmarkId.Value] = (traceId, spanId);
            var monitoringHome = EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");

            // if the monitoringHome is defined but invalid, we clean the monitoringHome variable.
            if (monitoringHome is not null && !Directory.Exists(monitoringHome))
            {
                monitoringHome = null;
            }

            string? profiler32Path = null, profiler64Path = null, ldPreload = null, loaderConfig = null;

            // if monitoring home is not defined, then we try to locate it in the default path using relative path from the benchmark assembly.
            monitoringHome ??= Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "..",
                "..",
                "..",
                "..",
                "..",
                "..",
                "..",
                "shared",
                "bin",
                "monitoring-home");

            // clean the path
            monitoringHome = Path.GetFullPath(monitoringHome);

            var osPlatform = FrameworkDescription.Instance.OSPlatform;
            var processArch = FrameworkDescription.Instance.ProcessArchitecture;
            if (string.Equals(osPlatform, OSPlatformName.Windows, StringComparison.OrdinalIgnoreCase))
            {
                // set required file paths
                profiler32Path = Path.Combine(monitoringHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");
                profiler64Path = Path.Combine(monitoringHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
                loaderConfig = Path.Combine(monitoringHome, "win-x64", "loader.conf");
                ldPreload = null;
            }
            else if (string.Equals(osPlatform, OSPlatformName.Linux, StringComparison.OrdinalIgnoreCase))
            {
                // set required file paths
                if (string.Equals(processArch, "arm64", StringComparison.OrdinalIgnoreCase))
                {
                    const string rid = "linux-arm64";
                    profiler32Path = null;
                    profiler64Path = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.so");
                    loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");
                    ldPreload = Path.Combine(monitoringHome, rid, "Datadog.Linux.ApiWrapper.x64.so");
                }
                else
                {
                    const string rid = "linux-x64";
                    profiler32Path = null;
                    profiler64Path = Path.Combine(monitoringHome, rid, "Datadog.Trace.ClrProfiler.Native.so");
                    loaderConfig = Path.Combine(monitoringHome, rid, "loader.conf");
                    ldPreload = Path.Combine(monitoringHome, rid, "Datadog.Linux.ApiWrapper.x64.so");
                }
            }
            else if (string.Equals(osPlatform, OSPlatformName.MacOS, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Datadog Profiler is not supported in macOS");
                return;
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
            if (!environment.TryGetValue(ConfigurationKeys.ServiceName, out _))
            {
                environment[ConfigurationKeys.ServiceName] = parameters.BenchmarkCase.Descriptor.FolderInfo;
            }

            if (!environment.TryGetValue(ConfigurationKeys.Environment, out _))
            {
                environment[ConfigurationKeys.Environment] = "benchmarks";
            }

            if (!environment.TryGetValue(ConfigurationKeys.ServiceVersion, out _))
            {
                environment[ConfigurationKeys.ServiceVersion] = CIEnvironmentValues.Instance.Branch;
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

            environment[ConfigurationKeys.CIVisibility.Enabled] = "1";
            environment["DD_CIVISIBILITY_BENCHMARK_TRACEID"] = traceId.ToString();
            environment["DD_CIVISIBILITY_BENCHMARK_SPANID"] = spanId.ToString();

            const string profilerEnabled = "DD_PROFILING_ENABLED";
            if (!environment.TryGetValue(profilerEnabled, out _))
            {
                environment[profilerEnabled] = "1";
            }

            const string profilerWalltimeEnabled = "DD_PROFILING_WALLTIME_ENABLED";
            if (!environment.TryGetValue(profilerWalltimeEnabled, out _))
            {
                environment[profilerWalltimeEnabled] = "1";
            }

            const string profilerCPUEnabled = "DD_PROFILING_CPU_ENABLED";
            if (!environment.TryGetValue(profilerCPUEnabled, out _))
            {
                environment[profilerCPUEnabled] = "1";
            }

            const string profilerAllocationEnabled = "DD_PROFILING_ALLOCATION_ENABLED";
            if (!environment.TryGetValue(profilerAllocationEnabled, out _))
            {
                environment[profilerAllocationEnabled] = "1";
            }

            const string profilerContentionEnabled = "DD_PROFILING_CONTENTION_ENABLED";
            if (!environment.TryGetValue(profilerContentionEnabled, out _))
            {
                environment[profilerContentionEnabled] = "1";
            }

            const string profilerExceptionEnabled = "DD_PROFILING_EXCEPTION_ENABLED";
            if (!environment.TryGetValue(profilerExceptionEnabled, out _))
            {
                environment[profilerExceptionEnabled] = "1";
            }

            const string profilerHeapEnabled = "DD_PROFILING_HEAP_ENABLED";
            if (!environment.TryGetValue(profilerHeapEnabled, out _))
            {
                environment[profilerHeapEnabled] = "1";
            }

            var settings = CIVisibility.Settings;
            const string profilerAgentless = "DD_PROFILING_AGENTLESS";
            if (!environment.TryGetValue(profilerAgentless, out _) && settings.Agentless)
            {
                environment[profilerAgentless] = "1";
            }
        }
    }

    public IEnumerable<Metric> ProcessResults(DiagnoserResults results)
    {
        return Enumerable.Empty<Metric>();
    }

    public void DisplayResults(ILogger logger)
    {
        logger.WriteLine("Datadog Profiler, profiles sent.");
    }

    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters)
    {
        return Enumerable.Empty<ValidationError>();
    }
}
