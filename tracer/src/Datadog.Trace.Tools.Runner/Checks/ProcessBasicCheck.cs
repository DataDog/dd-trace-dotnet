// <copyright file="ProcessBasicCheck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                Utils.WriteWarning(runtime == ProcessInfo.Runtime.Mixed ? BothRuntimesDetected : RuntimeDetectionFailed);
                runtime = ProcessInfo.Runtime.NetFx;
            }

            if (FindProfilerModule(process) == null)
            {
                Utils.WriteWarning(ProfilerNotLoaded);
                foundIssue = true;
            }

            var tracerModules = FindTracerModules(process).ToArray();

            if (tracerModules.Length == 0)
            {
                Utils.WriteWarning(TracerNotLoaded);
                foundIssue = true;
            }
            else if (tracerModules.Length > 1)
            {
                // There are too many tracers in there. Find out if it's bad or very bad
                bool areAllVersion2 = true;
                var versions = new HashSet<string>();

                foreach (var tracer in tracerModules)
                {
                    var version = FileVersionInfo.GetVersionInfo(tracer);

                    versions.Add(version.FileVersion ?? "{empty}");

                    if (version.FileMajorPart < 2)
                    {
                        areAllVersion2 = false;
                    }
                }

                Utils.WriteWarning(MultipleTracers(versions));

                if (!areAllVersion2)
                {
                    Utils.WriteError(VersionConflict);
                    foundIssue = true;
                }
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

        private static string? FindProfilerModule(ProcessInfo process)
        {
            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("Datadog.Trace.ClrProfiler.Native.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("Datadog.Trace.ClrProfiler.Native.so", StringComparison.OrdinalIgnoreCase))
                {
                    return module;
                }
            }

            return null;
        }

        private static IEnumerable<string> FindTracerModules(ProcessInfo process)
        {
            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("Datadog.Trace.dll", StringComparison.OrdinalIgnoreCase))
                {
                    yield return module;
                }
            }
        }
    }
}
