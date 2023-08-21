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
            Version? nativeTracerVersion = null;

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

            var loaderModule = FindLoader(process);
            var nativeTracerModule = FindNativeTracerModule(process, loaderModule != null);

            if (loaderModule == null)
            {
                AnsiConsole.WriteLine(LoaderNotLoaded);
            }

            if (nativeTracerModule == null)
            {
                Utils.WriteWarning(NativeTracerNotLoaded);
                ok = false;
            }
            else
            {
                // Only check the version of the native binary on Windows
                // .so modules don't have version metadata
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!Version.TryParse(FileVersionInfo.GetVersionInfo(nativeTracerModule).FileVersion, out nativeTracerVersion))
                    {
                        nativeTracerVersion = null;
                    }

                    AnsiConsole.WriteLine(ProfilerVersion(nativeTracerVersion != null ? $"{nativeTracerVersion}" : "{empty}"));
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

            string corProfilerPathKey = runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH";

            process.EnvironmentVariables.TryGetValue(corProfilerPathKey, out var corProfilerPathValue);

            bool isTracingUsingBundle = TracingWithBundle(corProfilerPathValue);

            if (isTracingUsingBundle)
            {
                AnsiConsole.WriteLine(TracingWithBundle);
                AnsiConsole.WriteLine(d);
            }
            else
            {
                AnsiConsole.WriteLine(TracingWithInstaller);

                ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH_32" : "COR_PROFILER_PATH_32", requiredOnLinux: false);
                ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH_64" : "COR_PROFILER_PATH_64", requiredOnLinux: false);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!CheckRegistry(registryService, nativeTracerVersion))
                    {
                        ok = false;
                    }
                }
            }

            ok &= CheckProfilerPath(process, runtime == ProcessInfo.Runtime.NetCore ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH", requiredOnLinux: true);

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

            if (process.EnvironmentVariables.TryGetValue("DD_TRACE_ENABLED", out var traceEnabledValue))
            {
                if (!ParseBooleanConfigurationValue(traceEnabledValue))
                {
                    Utils.WriteError(TracerNotEnabled(traceEnabledValue));
                }
            }

            bool isContinuousProfilerEnabled;

            if (process.EnvironmentVariables.TryGetValue("DD_PROFILING_ENABLED", out var profilingEnabled))
            {
                if (ParseBooleanConfigurationValue(profilingEnabled))
                {
                    AnsiConsole.WriteLine(ContinuousProfilerEnabled);
                    isContinuousProfilerEnabled = true;
                }
                else
                {
                    AnsiConsole.WriteLine(ContinuousProfilerDisabled);
                    isContinuousProfilerEnabled = false;
                }
            }
            else
            {
                AnsiConsole.WriteLine(ContinuousProfilerNotSet);
                isContinuousProfilerEnabled = false;
            }

            if (isContinuousProfilerEnabled)
            {
                ok &= CheckContinuousProfiler(process, loaderModule);
            }

            return ok;
        }

        internal static bool CheckContinuousProfiler(ProcessInfo process, string? loaderModule)
        {
            bool ok = true;

            var continuousProfilerModule = FindContinuousProfilerModule(process);

            if (continuousProfilerModule == null)
            {
                Utils.WriteWarning(ContinuousProfilerNotLoaded);
                ok = false;
            }

            if (loaderModule == null)
            {
                Utils.WriteError(ContinuousProfilerWithoutLoader);
                ok = false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.EnvironmentVariables.TryGetValue("LD_PRELOAD", out var ldPreload);

                if (ldPreload == null)
                {
                    Utils.WriteError(LdPreloadNotSet);
                    ok = false;
                }
                else
                {
                    if (Path.GetFileName(ldPreload) != "Datadog.Linux.ApiWrapper.x64.so")
                    {
                        Utils.WriteError(WrongLdPreload(ldPreload));
                        ok = false;
                    }
                    else if (!File.Exists(ldPreload))
                    {
                        Utils.WriteError(ApiWrapperNotFound(ldPreload));
                        ok = false;
                    }
                }
            }

            return ok;
        }

        internal static bool CheckRegistry(IRegistryService? registry = null, Version? tracerVersion = null)
        {
            registry ??= new Windows.RegistryService();

            try
            {
                bool ok = true;

                // Check that the profiler is properly registered
                if (tracerVersion == null || tracerVersion < new Version("2.14.0.0"))
                {
                    ok &= CheckClsid(registry, ClsidKey);
                    ok &= CheckClsid(registry, Clsid32Key);
                }

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
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

        private static string? FindLoader(ProcessInfo process)
        {
            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("Datadog.Trace.ClrProfiler.Native.dll", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("Datadog.Trace.ClrProfiler.Native.so", StringComparison.Ordinal))
                {
                    // This could be either the native tracer or the loader.
                    // If it's the loader then there should be a loader.conf file next to it.
                    var folder = Path.GetDirectoryName(module)!;

                    if (File.Exists(Path.Combine(folder, "loader.conf")))
                    {
                        return module;
                    }
                }
                else if (fileName.Equals("Datadog.AutoInstrumentation.NativeLoader.x64.dll", StringComparison.OrdinalIgnoreCase)
                      || fileName.Equals("Datadog.AutoInstrumentation.NativeLoader.x86.dll", StringComparison.OrdinalIgnoreCase)
                      || fileName.Equals("Datadog.AutoInstrumentation.NativeLoader.so", StringComparison.Ordinal))
                {
                    return module;
                }
            }

            return null;
        }

        private static string? FindContinuousProfilerModule(ProcessInfo process)
        {
            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals("Datadog.Profiler.Native.dll", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("Datadog.Profiler.Native.so", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("Datadog.AutoInstrumentation.Profiler.Native.x64.dll", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("Datadog.AutoInstrumentation.Profiler.Native.x86.dll", StringComparison.OrdinalIgnoreCase))
                {
                    return module;
                }
            }

            return null;
        }

        private static string? FindNativeTracerModule(ProcessInfo process, bool foundLoader)
        {
            var expectedFileName = foundLoader ? "Datadog.Tracer.Native" : "Datadog.Trace.ClrProfiler.Native";

            foreach (var module in process.Modules)
            {
                var fileName = Path.GetFileName(module);

                if (fileName.Equals($"{expectedFileName}.dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals($"{expectedFileName}.so", StringComparison.OrdinalIgnoreCase))
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

        private static bool ParseBooleanConfigurationValue(string value)
        {
            var trimmedValue = value.Trim();

            return trimmedValue is "true"
                or "True"
                or "TRUE"
                or "yes"
                or "Yes"
                or "YES"
                or "t"
                or "T"
                or "Y"
                or "y"
                or "1";
        }

        private static bool TracingWithBundle(string? profilerPathValue)
        {
            if (profilerPathValue is null)
            {
                return false;
            }

            string[] expectedEndingsForBundleSetup =
            {
                "/datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so",
                "/datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                "/datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so",
                "\\datadog\\win-x64\\Datadog.Trace.ClrProfiler.Native.dll",
                "\\datadog\\win-x86\\Datadog.Trace.ClrProfiler.Native.dll"
            };

            foreach (var bundleSetupEnding in expectedEndingsForBundleSetup)
            {
                if (profilerPathValue.EndsWith(bundleSetupEnding))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
