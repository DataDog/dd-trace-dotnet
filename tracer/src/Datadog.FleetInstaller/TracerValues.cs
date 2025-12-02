// <copyright file="TracerValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace Datadog.FleetInstaller;

internal sealed class TracerValues
{
    public TracerValues(string tracerHomeDirectory)
    {
        TracerHomeDirectory = tracerHomeDirectory;
        NativeLoaderX86Path = Path.Combine(tracerHomeDirectory, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll");
        NativeLoaderX64Path = Path.Combine(tracerHomeDirectory, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll");
        TelemetryForwarderPath = PathHelper.GetTelemetryForwarderPath(tracerHomeDirectory);
        IisRequiredEnvVariables = new(new Dictionary<string, string>
        {
            { "COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" },
            { "CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" },
            { "COR_PROFILER_PATH_32", NativeLoaderX86Path },
            { "CORECLR_PROFILER_PATH_32", NativeLoaderX86Path },
            { "COR_PROFILER_PATH_64", NativeLoaderX64Path },
            { "CORECLR_PROFILER_PATH_64", NativeLoaderX64Path },
            { "DD_DOTNET_TRACER_HOME", TracerHomeDirectory },
            { "COR_ENABLE_PROFILING", "1" },
            { "CORECLR_ENABLE_PROFILING", "1" },
            { "DD_INJECTION_ENABLED", "tracer" },
            { "DD_TELEMETRY_FORWARDER_PATH", TelemetryForwarderPath },
            { Defaults.InstrumentationInstallTypeKey, Defaults.InstrumentationInstallTypeValue },
        });

        // We don't enable the .NET FX environment variable globally for the global installation,
        // but we add the remainder of the variables for simplicity.
        // Note that this will technically make _all_ instrumentations on the host _look_ like SSI
        // even though we are not directly injecting
        GlobalRequiredEnvVariables = new(new Dictionary<string, string>
        {
            { "COR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" },
            { "CORECLR_PROFILER", "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}" },
            { "COR_PROFILER_PATH_32", NativeLoaderX86Path },
            { "CORECLR_PROFILER_PATH_32", NativeLoaderX86Path },
            { "COR_PROFILER_PATH_64", NativeLoaderX64Path },
            { "CORECLR_PROFILER_PATH_64", NativeLoaderX64Path },
            { "DD_DOTNET_TRACER_HOME", TracerHomeDirectory },
            // { "COR_ENABLE_PROFILING", "1" },
            { "CORECLR_ENABLE_PROFILING", "1" },
            { "DD_TRACING_ENABLED", "tracing" },
            { "DD_TELEMETRY_FORWARDER_PATH", TelemetryForwarderPath },
            { Defaults.InstrumentationInstallTypeKey, Defaults.InstrumentationInstallTypeValue },
        });

        FilesToAddToGac =
        [
            Path.Combine(tracerHomeDirectory, "net461", "Datadog.Trace.dll"),
            Path.Combine(tracerHomeDirectory, "net461", "Datadog.Trace.MSBuild.dll"),
        ];
    }

    public string TracerHomeDirectory { get; }

    public string NativeLoaderX86Path { get; }

    public string NativeLoaderX64Path { get; }

    public string TelemetryForwarderPath { get; }

    public ReadOnlyDictionary<string, string> IisRequiredEnvVariables { get; }

    public ReadOnlyDictionary<string, string> GlobalRequiredEnvVariables { get; }

    public ICollection<string> FilesToAddToGac { get; }
}
