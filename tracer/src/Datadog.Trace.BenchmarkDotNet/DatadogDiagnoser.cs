// <copyright file="DatadogDiagnoser.cs" company="Datadog">
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
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.BenchmarkDotNet;

/// <summary>
/// Datadog BenchmarkDotNet Diagnoser
/// </summary>
public class DatadogDiagnoser : IDiagnoser
{
    private readonly bool _enableProfiler;
    private PlatformNotSupportedException? _platformNotSupportedException;
    private string? _monitoringHome;
    private string? _profiler32Path;
    private string? _profiler64Path;
    private string? _ldPreload;
    private string? _loaderConfig;

    /// <summary>
    /// Default DatadogDiagnoser instance
    /// </summary>
    public static readonly IDiagnoser Default = new DatadogDiagnoser();

    /// <summary>
    /// Initializes a new instance of the <see cref="DatadogDiagnoser"/> class.
    /// </summary>
    public DatadogDiagnoser()
        : this(false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatadogDiagnoser"/> class.
    /// </summary>
    /// <param name="enableProfiler">True to enable Datadog's Profiler</param>
    public DatadogDiagnoser(bool enableProfiler)
    {
        _enableProfiler = enableProfiler;
    }

    /// <inheritdoc />
    public IEnumerable<string> Ids { get; } = new[] { "Datadog" };

    /// <inheritdoc />
    public IEnumerable<IExporter> Exporters { get; } = new[] { DatadogExporter.Default };

    /// <inheritdoc />
    public IEnumerable<IAnalyser> Analysers { get; } = Array.Empty<IAnalyser>();

    /// <inheritdoc />
    public RunMode GetRunMode(BenchmarkCase benchmarkCase) => _enableProfiler && !ShouldIgnoreProfiler(benchmarkCase) ? RunMode.ExtraRun : RunMode.NoOverhead;

    /// <inheritdoc />
    public bool RequiresBlockingAcknowledgments(BenchmarkCase benchmarkCase) => false;

    /// <inheritdoc />
    public void Handle(HostSignal signal, DiagnoserActionParameters parameters)
    {
        var utcNow = DateTime.UtcNow;
        switch (signal)
        {
            case HostSignal.BeforeAnythingElse:
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase, utcNow);
                break;
            case HostSignal.BeforeProcessStart:
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase.Descriptor.Type.Assembly, utcNow);
                BenchmarkMetadata.SetStartTime(parameters.BenchmarkCase.Descriptor.Type, utcNow);
                if (_enableProfiler && !ShouldIgnoreProfiler(parameters.BenchmarkCase))
                {
                    // Use Datadog's Profiler
                    EnsureAndFillProfilerPathVariables(parameters);
                    if (_platformNotSupportedException is null)
                    {
                        SetEnvironmentVariables(parameters, _monitoringHome, _profiler32Path, _profiler64Path, _ldPreload, _loaderConfig);
                    }
                }

                break;
            case HostSignal.AfterProcessExit:
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase.Descriptor.Type.Assembly, utcNow);
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase.Descriptor.Type, utcNow);
                break;
            case HostSignal.AfterAll:
                BenchmarkMetadata.SetEndTime(parameters.BenchmarkCase, utcNow);
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Metric> ProcessResults(DiagnoserResults results) => Array.Empty<Metric>();

    /// <inheritdoc />
    public void DisplayResults(ILogger logger)
    {
        if (_enableProfiler)
        {
            if (_platformNotSupportedException is null)
            {
                logger.WriteLine($"Datadog Profiler was attached.");
            }
            else
            {
                logger.WriteLine("Datadog Profiler: " + _platformNotSupportedException.Message);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<ValidationError> Validate(ValidationParameters validationParameters) => Array.Empty<ValidationError>();

    private static IEnumerable<string?> GetHomeFolderPaths(DiagnoserActionParameters parameters)
    {
        // try to locate it from the environment variable
        yield return EnvironmentHelpers.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");

        // try to locate it inside the running benchmark folder
        yield return Path.Combine(parameters.Process.StartInfo.WorkingDirectory, "datadog");

        // try to locate it inside the benchmark folder
        yield return Path.Combine(Path.GetDirectoryName(parameters.BenchmarkCase.Descriptor.Type.Assembly.Location) ?? string.Empty, "datadog");

#if LOCAL_HOME_FOLDER
        // try to locate it in the default path using relative path from the benchmark assembly.
        yield return Path.Combine(
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
#endif
    }

    private static bool GetPaths(string monitoringHome, ref string? profiler32Path, ref string? profiler64Path, ref string? loaderConfig, ref string? ldPreload)
    {
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
            throw new PlatformNotSupportedException("Datadog Profiler is not supported in macOS");
        }

        if (!File.Exists(profiler64Path) ||
            (profiler32Path is not null && !File.Exists(profiler64Path)) ||
            !File.Exists(loaderConfig))
        {
            return false;
        }

        return true;
    }

    private static void SetEnvironmentVariables(DiagnoserActionParameters parameters, string? monitoringHome, string? profiler32Path, string? profiler64Path, string? ldPreload, string? loaderConfig)
    {
        BenchmarkMetadata.GetIds(parameters.BenchmarkCase, out var traceId, out var spanId);
        var tracer = Tracer.Instance;
        var environment = parameters.Process.StartInfo.Environment;
        if (!environment.TryGetValue(ConfigurationKeys.ServiceName, out _))
        {
            environment[ConfigurationKeys.ServiceName] = tracer.DefaultServiceName;
        }

        if (!environment.TryGetValue(ConfigurationKeys.Environment, out _))
        {
            environment[ConfigurationKeys.Environment] = tracer.Settings.EnvironmentInternal;
        }

        if (!environment.TryGetValue(ConfigurationKeys.ServiceVersion, out _))
        {
            environment[ConfigurationKeys.ServiceVersion] = tracer.Settings.ServiceVersionInternal;
        }

        const string ProfilerId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        environment["COR_ENABLE_PROFILING"] = "1";
        environment["CORECLR_ENABLE_PROFILING"] = "1";
        environment["COR_PROFILER"] = ProfilerId;
        environment["CORECLR_PROFILER"] = ProfilerId;
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

        // CI Visibility integration environment variables
        environment[ConfigurationKeys.CIVisibility.Enabled] = "1";
        environment["DD_INTERNAL_CIVISIBILITY_RUNTIMEID"] = RuntimeId.Get();
        environment["DD_INTERNAL_CIVISIBILITY_SPANID"] = spanId.ToString();

        // Profiler options
        const string profilerEnabled = "DD_PROFILING_ENABLED";
        if (!environment.TryGetValue(profilerEnabled, out _))
        {
            environment[profilerEnabled] = "1";
        }

        const string profilerCPUEnabled = "DD_PROFILING_CPU_ENABLED";
        if (!environment.TryGetValue(profilerCPUEnabled, out _))
        {
            environment[profilerCPUEnabled] = "1";
        }

        const string profilerWalltimeEnabled = "DD_PROFILING_WALLTIME_ENABLED";
        if (!environment.TryGetValue(profilerWalltimeEnabled, out _))
        {
            environment[profilerWalltimeEnabled] = "1";
        }

        const string profilerExceptionEnabled = "DD_PROFILING_EXCEPTION_ENABLED";
        if (!environment.TryGetValue(profilerExceptionEnabled, out _))
        {
            environment[profilerExceptionEnabled] = "1";
        }

        const string profilerAllocationEnabled = "DD_PROFILING_ALLOCATION_ENABLED";
        if (!environment.TryGetValue(profilerAllocationEnabled, out _))
        {
            environment[profilerAllocationEnabled] = "1";
        }

        const string profilerLockEnabled = "DD_PROFILING_LOCK_ENABLED";
        if (!environment.TryGetValue(profilerLockEnabled, out _))
        {
            environment[profilerLockEnabled] = "1";
        }

        const string profilerGcEnabled = "DD_PROFILING_GC_ENABLED";
        if (!environment.TryGetValue(profilerGcEnabled, out _))
        {
            environment[profilerGcEnabled] = "1";
        }

        const string profilerHeapEnabled = "DD_PROFILING_HEAP_ENABLED";
        if (!environment.TryGetValue(profilerHeapEnabled, out _))
        {
            environment[profilerHeapEnabled] = "1";
        }

        environment["DD_PROFILING_AGENTLESS"] = CIVisibility.Settings.Agentless ? "1" : "0";
        environment["DD_PROFILING_UPLOAD_PERIOD"] = "90";
        environment["DD_INTERNAL_PROFILING_SAMPLING_RATE"] = "1";
        environment["DD_INTERNAL_PROFILING_WALLTIME_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CODEHOTSPOTS_THREADS_THRESHOLD"] = "64";
        environment["DD_INTERNAL_PROFILING_CPUTIME_THREADS_THRESHOLD"] = "128";
        environment["DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED "] = "1";
        environment["DD_PROFILING_FRAMES_NATIVE_ENABLED "] = "1";

        // Tags
        var tagsList = new List<string>
        {
            { $"origin:{TestTags.CIAppTestOriginName}" },
        };

        // Git data
        if (CIEnvironmentValues.Instance is { } ciEnv)
        {
            environment["DD_GIT_REPOSITORY_URL"] = ciEnv.Repository;
            environment["DD_GIT_COMMIT_SHA"] = ciEnv.Commit;

            if (!string.IsNullOrEmpty(ciEnv.Branch))
            {
                tagsList.Add($"{CommonTags.GitBranch}:{ciEnv.Branch}");
            }

            if (!string.IsNullOrEmpty(ciEnv.Tag))
            {
                tagsList.Add($"{CommonTags.GitTag}:{ciEnv.Tag}");
            }
        }

        var newDdTags = string.Join(", ", tagsList);
        if (environment.TryGetValue("DD_TAGS", out var ddTags))
        {
            environment["DD_TAGS"] = newDdTags + "," + ddTags;
        }
        else
        {
            environment["DD_TAGS"] = newDdTags;
        }
    }

    private void EnsureAndFillProfilerPathVariables(DiagnoserActionParameters parameters)
    {
        if (!string.IsNullOrEmpty(_monitoringHome))
        {
            return;
        }

        try
        {
            foreach (var homePath in GetHomeFolderPaths(parameters))
            {
                if (string.IsNullOrEmpty(homePath) || !Directory.Exists(homePath))
                {
                    continue;
                }

                var tmpHomePath = Path.GetFullPath(homePath);
                if (GetPaths(tmpHomePath, ref _profiler32Path, ref _profiler64Path, ref _loaderConfig, ref _ldPreload))
                {
                    _monitoringHome = tmpHomePath;
                    break;
                }
            }
        }
        catch (PlatformNotSupportedException platformNotSupportedException)
        {
            // Store the exception if the platform is not supported and ignore everything.
            _platformNotSupportedException = platformNotSupportedException;
            return;
        }

        if (!File.Exists(_profiler64Path))
        {
            throw new FileNotFoundException(null, _profiler64Path);
        }

        if (_profiler32Path is not null && !File.Exists(_profiler32Path))
        {
            throw new FileNotFoundException(null, _profiler32Path);
        }

        if (!File.Exists(_loaderConfig))
        {
            throw new FileNotFoundException(null, _loaderConfig);
        }
    }

    private bool ShouldIgnoreProfiler(BenchmarkCase benchmarkCase)
    {
        if (benchmarkCase?.Descriptor is { } descriptor)
        {
            var attributes = descriptor.Type?.GetCustomAttributes() ?? [];
            attributes = attributes.Concat(descriptor.WorkloadMethod?.GetCustomAttributes() ?? []);
            foreach (var attribute in attributes)
            {
                if (attribute is IgnoreProfileAttribute)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
