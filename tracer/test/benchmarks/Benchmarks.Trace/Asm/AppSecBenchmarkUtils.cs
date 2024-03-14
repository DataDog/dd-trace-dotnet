// <copyright file="AppSecBenchmarkUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace.Asm;

internal class AppSecBenchmarkUtils
{
    internal static void SetupDummyAgent()
    {
        var settings = new TracerSettings { StartupDiagnosticLogEnabled = false, MaxTracesSubmittedPerSecond = 0 };
        Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));
    }

    internal static WafLibraryInvoker CreateWafLibraryInvoker()
    {
        var fDesc = FrameworkDescription.Instance;
        var rid = (fDesc.ProcessArchitecture, fDesc.OSPlatform) switch
        {
            ("x64", "Windows") => "win-x64",
            ("x86", "Windows") => "win-x86",
            ("x64", "Linux") => "linux-x64",
            ("arm64", "Linux") => "linux-arm64",
            _ => throw new Exception($"RID not detected or supported: {fDesc.OSPlatform} / {fDesc.ProcessArchitecture}")
        };

        var folder = new DirectoryInfo(Environment.CurrentDirectory);
        var path = Environment.CurrentDirectory;
        while (folder.Exists)
        {
            path = Path.Combine(folder.FullName, "./shared/bin/monitoring-home");
            if (Directory.Exists(path))
            {
                break;
            }

            if (folder == folder.Parent)
            {
                break;
            }

            folder = folder.Parent;
        }

        path = Path.Combine(path, $"./{rid}/");
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The Path: '{path}' doesn't exist.");
        }

        Environment.SetEnvironmentVariable("DD_TRACE_LOGGING_RATE", "60");
        Environment.SetEnvironmentVariable("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH", path);
        var libInitResult = WafLibraryInvoker.Initialize();
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        return libInitResult.WafLibraryInvoker!;
    }
}
