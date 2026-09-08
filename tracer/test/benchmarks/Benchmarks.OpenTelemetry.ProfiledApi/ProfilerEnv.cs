using System;
using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Jobs;

namespace Benchmarks.OpenTelemetry.ProfiledApi;

/// <summary>
/// Resolves the local monitoring-home layout and builds the two BDN jobs ("listener" and "interception")
/// that attach the native Datadog profiler to only the measured child process, via
/// Job.WithEnvironmentVariable(s) - not the DatadogDiagnoser, whose RunMode.ExtraRun results are discarded
/// under `--launchCount 5`. Deliberately has zero references to Datadog.Trace(.*): see the csproj comment on
/// why this project must stay reference-free. Windows-only for now (win-x64); extend ResolvePaths for
/// linux-x64/linux-arm64 (+ LD_PRELOAD) when this needs to run in CI.
/// </summary>
internal static class ProfilerEnv
{
    private const string ProfilerClsid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

    public static Job CreateListenerJob(string id) => CreateProfiledJob(id, interceptionEnabled: false);

    public static Job CreateInterceptionJob(string id) => CreateProfiledJob(id, interceptionEnabled: true);

    private static Job CreateProfiledJob(string id, bool interceptionEnabled)
    {
        var monitoringHome = ResolveMonitoringHome();
        var (profilerPath, loaderConfig) = ResolvePaths(monitoringHome);

        return Job.Default
                  .WithId(id)
                  .WithEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1")
                  .WithEnvironmentVariable("CORECLR_PROFILER", ProfilerClsid)
                  .WithEnvironmentVariable("CORECLR_PROFILER_PATH_64", profilerPath)
                  .WithEnvironmentVariable("DD_DOTNET_TRACER_HOME", monitoringHome)
                  .WithEnvironmentVariable("DD_NATIVELOADER_CONFIGFILE", loaderConfig)
                  // Mandatory in both arms: without it, and with no other OTel subscriber, StartActivity
                  // returns null and every benchmark degenerates into a null check - the listener arm would
                  // look spectacularly (and wrongly) fast. Set identically so the arms differ in exactly
                  // one variable.
                  .WithEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true")
                  .WithEnvironmentVariable("DD_TRACE_OTEL_ACTIVITY_INTERCEPTION_ENABLED", interceptionEnabled ? "true" : "false")
                  .WithEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "false")
                  .WithEnvironmentVariable("DD_REMOTE_CONFIGURATION_ENABLED", "false")
                  .WithEnvironmentVariable("DD_INTERNAL_AGENT_STANDALONE_MODE_ENABLED", "true")
                  .WithEnvironmentVariable("DD_TRACE_STARTUP_LOGS", "0")
                  .WithEnvironmentVariable("DD_PROFILING_ENABLED", "0");
    }

    private static string ResolveMonitoringHome()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
        if (!string.IsNullOrEmpty(fromEnv) && Directory.Exists(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        // UseArtifactsOutput layout: artifacts\bin\<Project>\<config>_<tfm>\ -> artifacts\monitoring-home
        var assemblyDir = Path.GetDirectoryName(typeof(ProfilerEnv).Assembly.Location) ?? string.Empty;
        var candidate = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "monitoring-home"));
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        throw new InvalidOperationException(
            "ProfilerEnv: could not locate the monitoring-home folder. Set DD_DOTNET_TRACER_HOME, or run " +
            "`.\\tracer\\build.cmd BuildTracerHome BuildNativeLoader` first. " +
            $"Checked '{fromEnv}' and '{candidate}'.");
    }

    private static (string ProfilerPath, string LoaderConfig) ResolvePaths(string monitoringHome)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("ProfilerEnv only resolves win-x64 paths today - extend it for Linux before running there.");
        }

        var profilerPath = Path.Combine(monitoringHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
        var loaderConfig = Path.Combine(monitoringHome, "win-x64", "loader.conf");

        if (!File.Exists(profilerPath))
        {
            throw new FileNotFoundException("Native profiler not found - build it with `.\\tracer\\build.cmd BuildNativeLoader`.", profilerPath);
        }

        if (!File.Exists(loaderConfig))
        {
            throw new FileNotFoundException("loader.conf not found - build the tracer home with `.\\tracer\\build.cmd BuildTracerHome`.", loaderConfig);
        }

        return (profilerPath, loaderConfig);
    }
}
