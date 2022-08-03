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
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Spectre.Console;

using static Datadog.Trace.Tools.Runner.Checks.Resources;

namespace Datadog.Trace.Tools.Runner.Checks
{
    internal class ProcessBasicCheck
    {
        internal const string ClsidKey = @"SOFTWARE\Classes\CLSID\" + Utils.Profilerid + @"\InprocServer32";
        internal const string Clsid32Key = @"SOFTWARE\Classes\Wow6432Node\CLSID\" + Utils.Profilerid + @"\InprocServer32";

        public static bool Run(ProcessInfo process, IRegistryService? registryService = null)
        {
            bool ok = true;
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

            var profilerModule = FindProfilerModule(process);

            if (profilerModule == null)
            {
                Utils.WriteWarning(ProfilerNotLoaded);
                ok = false;
            }
            else
            {
                // Only check the version of the native binary on Windows
                // .so modules don't have version metadata
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var version = FileVersionInfo.GetVersionInfo(profilerModule);
                    AnsiConsole.WriteLine(ProfilerVersion(version.FileVersion ?? "{empty}"));
                }
            }

            var tracerModules = FindTracerModules(process).ToArray();

            if (tracerModules.Length == 0)
            {
                Utils.WriteWarning(TracerNotLoaded);
                ok = false;
            }
            else if (tracerModules.Length == 1)
            {
                var version = FileVersionInfo.GetVersionInfo(tracerModules[0]);
                AnsiConsole.WriteLine(TracerVersion(version.FileVersion ?? "{empty}"));
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
                    ok = false;
                }
            }

            if (process.EnvironmentVariables.TryGetValue("DD_DOTNET_TRACER_HOME", out var tracerHome))
            {
                if (!Directory.Exists(tracerHome))
                {
                    Utils.WriteWarning(TracerHomeNotFoundFormat(tracerHome));
                    ok = false;
                }
            }
            else
            {
                Utils.WriteWarning(EnvironmentVariableNotSet("DD_DOTNET_TRACER_HOME"));
                ok = false;
            }

            string corProfilerKey = runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER" : "COR_PROFILER";

            process.EnvironmentVariables.TryGetValue(corProfilerKey, out var corProfiler);

            if (corProfiler != Utils.Profilerid)
            {
                Utils.WriteWarning(WrongEnvironmentVariableFormat(corProfilerKey, Utils.Profilerid, corProfiler));
                ok = false;
            }

            string corEnableKey = runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_ENABLE_PROFILING" : "COR_ENABLE_PROFILING";

            process.EnvironmentVariables.TryGetValue(corEnableKey, out var corEnable);

            if (corEnable != "1")
            {
                Utils.WriteError(WrongEnvironmentVariableFormat(corEnableKey, "1", corEnable));
                ok = false;
            }

            ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH", requiredOnLinux: true);
            ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH_32" : "COR_PROFILER_PATH_32", requiredOnLinux: false);
            ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH_64" : "COR_PROFILER_PATH_64", requiredOnLinux: false);

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                if (!CheckRegistry(registryService))
                {
                    ok = false;
                }
            }

            return ok;
        }

        internal static bool CheckRegistry(IRegistryService? registry = null)
        {
            registry ??= new Windows.RegistryService();

            try
            {
                bool ok = true;

                // Check that the profiler is properly registered
                ok &= CheckClsid(registry, ClsidKey);
                ok &= CheckClsid(registry, Clsid32Key);

                // Look for registry keys that could have been set by other profilers
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

                var parentKeys = new[] { @"SOFTWARE\Microsoft\.NETFramework", @"SOFTWARE\WOW6432Node\Microsoft\.NETFramework" };

                foreach (var parentKey in parentKeys)
                {
                    foreach (var name in registry.GetLocalMachineValueNames(parentKey))
                    {
                        if (suspiciousNames.Contains(name))
                        {
                            Utils.WriteWarning(SuspiciousRegistryKey(parentKey, name));
                            foundKey = true;
                        }
                    }
                }

                ok &= !foundKey;

                return ok;
            }
            catch (Exception ex)
            {
                Utils.WriteError(ErrorCheckingRegistry(ex.Message));
                return true;
            }
        }

        private static bool CheckProfilerPath(ProcessInfo process, string key, bool requiredOnLinux)
        {
            bool ok = true;

            if (process.EnvironmentVariables.TryGetValue(key, out var profilerPath))
            {
                if (!IsExpectedProfilerFile(profilerPath))
                {
                    Utils.WriteError(WrongProfilerEnvironment(key, profilerPath));
                    ok = false;
                }

                if (!File.Exists(profilerPath))
                {
                    Utils.WriteError(MissingProfilerEnvironment(key, profilerPath));
                    ok = false;
                }
            }
            else if (requiredOnLinux)
            {
                if (!RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    Utils.WriteError(EnvironmentVariableNotSet(key));
                    ok = false;
                }
            }

            return ok;
        }

        private static bool CheckClsid(IRegistryService registry, string registryKey)
        {
            var profilerPath = registry.GetLocalMachineValue(registryKey);

            if (profilerPath == null)
            {
                Utils.WriteWarning(MissingRegistryKey(registryKey));
                return false;
            }

            if (!IsExpectedProfilerFile(profilerPath))
            {
                Utils.WriteError(WrongProfilerRegistry(registryKey, profilerPath));
                return false;
            }

            if (!File.Exists(profilerPath))
            {
                Utils.WriteError(MissingProfilerRegistry(registryKey, profilerPath));
                return false;
            }

            return true;
        }

        private static bool IsExpectedProfilerFile(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return "Datadog.Trace.ClrProfiler.Native.dll".Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                       "Datadog.Tracer.Native.dll".Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                       // Check for legacy names
                       "Datadog.AutoInstrumentation.NativeLoader.x64.dll".Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                       "Datadog.AutoInstrumentation.NativeLoader.x86.dll".Equals(fileName, StringComparison.OrdinalIgnoreCase);
            }

            // Paths are case-sensitive on Linux
            return "Datadog.Trace.ClrProfiler.Native.so".Equals(fileName, StringComparison.Ordinal) ||
                   "Datadog.Tracer.Native.so".Equals(fileName, StringComparison.Ordinal) ||
                    // Check for legacy names
                   "Datadog.AutoInstrumentation.NativeLoader.so".Equals(fileName, StringComparison.Ordinal);
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
