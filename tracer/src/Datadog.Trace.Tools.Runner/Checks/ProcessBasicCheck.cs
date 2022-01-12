// <copyright file="ProcessBasicCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessBasicCheck
    {
        private enum Runtime
        {
            Unknown,
            NetFx,
            NetCore
        }

        public static bool Run(ProcessInfo process)
        {
            bool foundIssue = false;
            var runtime = DetectRuntime(process);

            if (runtime == Runtime.NetFx)
            {
                AnsiConsole.WriteLine("Target process is running with .NET Framework");
            }
            else if (runtime == Runtime.NetCore)
            {
                AnsiConsole.WriteLine("Target process is running with .NET Core");
            }
            else
            {
                Utils.WriteWarning("Failed to detect target process runtime, assuming .NET Framework");
                runtime = Runtime.NetFx;
            }

            var modules = FindTracerModules(process);

            if (modules.Profiler == null)
            {
                Utils.WriteWarning("Profiler is not loaded into the process");
                foundIssue = true;
            }

            if (modules.Tracer == null)
            {
                Utils.WriteWarning("Tracer is not loaded into the process");
                foundIssue = true;
            }

            if (process.EnvironmentVariables.TryGetValue("DD_DOTNET_TRACER_HOME", out var tracerHome))
            {
                if (!Directory.Exists(tracerHome))
                {
                    Utils.WriteWarning($"DD_DOTNET_TRACER_HOME is set to {tracerHome} but the directory does not exist");
                    foundIssue = true;
                }
            }
            else
            {
                Utils.WriteWarning("The environment variable DD_DOTNET_TRACER_HOME is not set");
                foundIssue = true;
            }

            string corProfilerKey = runtime == Runtime.NetCore ? "CORECLR_PROFILER" : "COR_PROFILER";

            process.EnvironmentVariables.TryGetValue(corProfilerKey, out var corProfiler);

            if (corProfiler != Utils.PROFILERID)
            {
                Utils.WriteWarning($"The environment variable {corProfilerKey} should be set to {Utils.PROFILERID} (current value: {corProfiler ?? "not set"})");
                foundIssue = true;
            }

            string corEnableKey = runtime == Runtime.NetCore ? "CORECLR_ENABLE_PROFILING" : "COR_ENABLE_PROFILING";

            process.EnvironmentVariables.TryGetValue(corEnableKey, out var corEnable);

            if (corEnable != "1")
            {
                Utils.WriteWarning($"The environment variable {corEnableKey} should be set to 1 (current value: {corEnable ?? "not set"})");
                foundIssue = true;
            }

            return !foundIssue;
        }

        private static Runtime DetectRuntime(ProcessInfo process)
        {
            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("clr", StringComparison.OrdinalIgnoreCase))
                {
                    return Runtime.NetFx;
                }

                if (fileName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("libcoreclr.so", StringComparison.OrdinalIgnoreCase))
                {
                    return Runtime.NetCore;
                }
            }

            return Runtime.Unknown;
        }

        private static (string Profiler, string Tracer) FindTracerModules(ProcessInfo process)
        {
            (string Profiler, string Tracer) result = default;

            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("datadog.trace.dll", StringComparison.OrdinalIgnoreCase))
                {
                    result.Tracer = fileName;
                }
                else if (fileName.Equals("Datadog.Trace.ClrProfiler.Native.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("Datadog.Trace.ClrProfiler.Native.so", StringComparison.OrdinalIgnoreCase))
                {
                    result.Profiler = fileName;
                }
            }

            return result;
        }
    }
}
