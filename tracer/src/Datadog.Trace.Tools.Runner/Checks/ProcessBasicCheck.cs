// <copyright file="ProcessBasicCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Spectre.Console;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessBasicCheck
    {
        public static bool Run(ProcessInfo process)
        {
            bool foundIssue = false;
            var runtime = process.DotnetRuntime;

            if (runtime == ProcessInfo.Runtime.NetFx)
            {
                AnsiConsole.WriteLine(NetFrameworkRuntime);
            }
            else if (runtime == ProcessInfo.Runtime.NetCore)
            {
                AnsiConsole.WriteLine(NetCoreRuntime);
            }
            else
            {
                Utils.WriteWarning(RuntimeDetectionFailed);
                runtime = ProcessInfo.Runtime.NetFx;
            }

            var modules = FindTracerModules(process);

            if (modules.Profiler == null)
            {
                Utils.WriteWarning(ProfilerNotLoaded);
                foundIssue = true;
            }

            if (modules.Tracer == null)
            {
                Utils.WriteWarning(TracerNotLoaded);
                foundIssue = true;
            }

            if (process.EnvironmentVariables.TryGetValue("DD_DOTNET_TRACER_HOME", out var tracerHome))
            {
                if (!Directory.Exists(tracerHome))
                {
                    Utils.WriteWarning(TracerHomeNotFoundFormat(tracerHome));
                    foundIssue = true;
                }
            }
            else
            {
                Utils.WriteWarning(TracerHomeNotSet);
                foundIssue = true;
            }

            string corProfilerKey = runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER" : "COR_PROFILER";

            process.EnvironmentVariables.TryGetValue(corProfilerKey, out var corProfiler);

            if (corProfiler != Utils.Profilerid)
            {
                Utils.WriteWarning(WrongEnvironmentVariableFormat(corProfilerKey, Utils.Profilerid, corProfiler));
                foundIssue = true;
            }

            string corEnableKey = runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_ENABLE_PROFILING" : "COR_ENABLE_PROFILING";

            process.EnvironmentVariables.TryGetValue(corEnableKey, out var corEnable);

            if (corEnable != "1")
            {
                Utils.WriteWarning(WrongEnvironmentVariableFormat(corEnableKey, "1", corEnable));
                foundIssue = true;
            }

            if (!CheckRegistry())
            {
                foundIssue = true;
            }

            return !foundIssue;
        }

        internal static bool CheckRegistry(IRegistryService? registry = null)
        {
            registry ??= new Windows.RegistryService();

            try
            {
                var suspiciousNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "COR_ENABLE_PROFILING",
                    "CORECLR_ENABLE_PROFILING",
                    "COR_PROFILER",
                    "CORECLR_PROFILER",
                    "COR_PROFILER_PATH",
                    "CORECLR_PROFILER_PATH",
                    "COR_PROFILER_PATH_64",
                    "CORECLR_PROFILER_PATH_64"
                };

                bool foundKey = false;

                foreach (var name in registry.GetLocalMachineValueNames(@"SOFTWARE\Microsoft\.NETFramework"))
                {
                    if (suspiciousNames.Contains(name))
                    {
                        Utils.WriteWarning(SuspiciousRegistryKey(name));
                        foundKey = true;
                    }
                }

                return !foundKey;
            }
            catch (Exception ex)
            {
                Utils.WriteError(ErrorCheckingRegistry(ex.Message));
                return true;
            }
        }

        private static (string? Profiler, string? Tracer) FindTracerModules(ProcessInfo process)
        {
            (string? Profiler, string? Tracer) result = default;

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
