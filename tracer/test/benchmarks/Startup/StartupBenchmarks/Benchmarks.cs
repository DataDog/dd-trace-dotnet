using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace StartupBenchmarks;

[SimpleJob]
public class Benchmarks
{
    public string TracerHomeDirectory { get; set; }

    public string TracingLibraryPath { get; set; }

    public string EntryAssemblyPath { get; set; }

    public string Os { get; set; }

    public string Arch { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        Os = GetOs();
        Arch = GetArch();

        switch (Os)
        {
            case "win":
                TracerHomeDirectory = @"C:\Users\Lucas.Pimentel\Downloads\tracer-home-3.9.1";
                TracingLibraryPath = $"{TracerHomeDirectory}/win-{Arch}/Datadog.Trace.ClrProfiler.Native.dll";
                EntryAssemblyPath = @"D:\source\datadog\dd-trace-dotnet\tracer\test\benchmarks\Startup\EmptyConsoleApp\publish\default\EmptyConsoleApp.dll";
                break;
            case "linux":
                TracerHomeDirectory = "/home/lucas/tracer-home-3.9.1";
                TracingLibraryPath = $"{TracerHomeDirectory}/linux-{Arch}/Datadog.Trace.ClrProfiler.Native.so";
                EntryAssemblyPath = "/home/lucas/source/datadog/dd-trace-dotnet/tracer/test/benchmarks/Startup/EmptyConsoleApp/publish/default/EmptyConsoleApp.dll";
                break;
            default:
                throw new PlatformNotSupportedException($"Platform not supported: {Os}-{Arch}");
        }

        if (!File.Exists(EntryAssemblyPath))
        {
            throw new FileNotFoundException($"Entry assembly file not found: {EntryAssemblyPath}", EntryAssemblyPath);
        }

        if (!Directory.Exists(TracerHomeDirectory))
        {
            throw new DirectoryNotFoundException($"Tracer home directory not found: {TracerHomeDirectory}");
        }
    }

    [Benchmark(Description = "Tracer disabled")]
    public void NoTracer()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment, enable: false);

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    [Benchmark(Baseline = true, Description = "Tracer enabled, default config (baseline)")]
    public void TracerEnabled()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment);

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    [Benchmark(Description = "DD_INSTRUMENTATION_TELEMETRY_ENABLED=0")]
    public void TracerEnabled_InstrumentationTelemetryDisabled()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment);
        EnableInstrumentationTelemetry(startInfo.Environment, enable: false);

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    [Benchmark(Description = "DD_CIVISIBILITY_ENABLED=0")]
    public void TracerEnabled_CiVisDisabled()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment);
        EnableCiVisibility(startInfo.Environment, enable: false);

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    [Benchmark(Description = "DD_TRACE_LOG_DIRECTORY=/dev/null")]
    public void TracerEnabled_LogToDevNull()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment);
        SetLoggingDirectory(startInfo.Environment, "/dev/null");

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    [Benchmark(Description = "Tracer enabled, all of the above")]
    public void TracerEnabled_All()
    {
        var startInfo = CreateProcessStartInfo();

        EnableTracer(startInfo.Environment);
        EnableInstrumentationTelemetry(startInfo.Environment, enable: false);
        EnableCiVisibility(startInfo.Environment, enable: false);
        SetLoggingDirectory(startInfo.Environment, "/dev/null");

        var process = Process.Start(startInfo);
        process?.WaitForExit();
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec {EntryAssemblyPath}",
        };
        return startInfo;
    }

    private void EnableTracer(IDictionary<string, string> startInfo, bool enable = true)
    {
        startInfo["CORECLR_ENABLE_PROFILING"] = enable ? "1" : "0";
        startInfo["CORECLR_PROFILER"] = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        startInfo["CORECLR_PROFILER_PATH"] = TracingLibraryPath;
        startInfo["DD_DOTNET_TRACER_HOME"] = TracerHomeDirectory;
    }

    private void EnableCiVisibility(IDictionary<string, string> startInfo, bool enable = true)
    {
        startInfo["DD_CIVISIBILITY_ENABLED"] = enable ? "1" : "0";
    }

    private void EnableInstrumentationTelemetry(IDictionary<string, string> startInfo, bool enable = true)
    {
        startInfo["DD_INSTRUMENTATION_TELEMETRY_ENABLED"] = enable ? "1" : "0";
    }

    private void SetLoggingDirectory(IDictionary<string, string> startInfo, string directory)
    {
        startInfo["DD_TRACE_LOG_DIRECTORY"] = directory;
    }

    private static string GetOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }

        throw new PlatformNotSupportedException($"OS platform not supported: {RuntimeInformation.OSDescription}");
    }

    private static string GetArch()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Process architecture not supported: {RuntimeInformation.OSArchitecture}"),
        };
    }
}
