// <copyright file="LibraryLocationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal static class LibraryLocationHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LibraryLocationHelper));

        internal static List<string> GetDatadogNativeFolders(string tracerHome, string traceNativeEnginePath, FrameworkDescription frameworkDescription, string[] runtimeIds)
        {
            // first get anything "home folder" like
            // if running under Windows:
            // - get msi install location
            // - then get the default program files folder (because of a know issue in the installer for location of the x86 folder on a 64bit OS)
            // if running under linux:
            //   - may have multiple runtime IDs (e.g. musl)
            // then combine with the profiler's location
            // taking into account that these locations could be the same place

            List<string> paths = new();

            // AddNativeLoaderEnginePath;
            // The native loader sets this env var to say where it's loaded from, so the waf should be next to it
            // Use this preferentially over other options
            if (!string.IsNullOrWhiteSpace(traceNativeEnginePath))
            {
                paths.Add(Path.GetDirectoryName(traceNativeEnginePath));
            }

            foreach (var runtimeId in runtimeIds)
            {
                AddHomeFolders(tracerHome, paths, runtimeId);
            }

            foreach (var runtimeId in runtimeIds)
            {
                AddProfilerFolders(paths, frameworkDescription, runtimeId);
            }

            return paths.Distinct().ToList();
        }

        private static void AddProfilerFolders(List<string> paths, FrameworkDescription frameworkDescription, string runtimeId)
        {
            var isCoreClr = frameworkDescription.IsCoreClr();
            // Try architecture-agnostic profiler path first
            var archAgnosticValue = isCoreClr
                ? EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetCoreClrProfiler)
                : EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetClrProfiler);

            if (!string.IsNullOrWhiteSpace(archAgnosticValue))
            {
                AddRuntimeSpecificLocations(paths, Path.GetDirectoryName(archAgnosticValue), runtimeId);
                return;
            }

            // Try architecture-specific profiler path
            string archSpecificValue;
            if (isCoreClr)
            {
                archSpecificValue = Environment.Is64BitProcess
                    ? EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetCoreClrProfiler64)
                    : EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetCoreClrProfiler32);
            }
            else
            {
                archSpecificValue = Environment.Is64BitProcess
                    ? EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetClrProfiler64)
                    : EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetClrProfiler32);
            }

            if (!string.IsNullOrWhiteSpace(archSpecificValue))
            {
                AddRuntimeSpecificLocations(paths, Path.GetDirectoryName(archSpecificValue), runtimeId);
            }
            else
            {
                // this is expected under Windows, but problematic under other OSs
                Log.Debug("Couldn't find profilerFolder");
            }
        }

        private static void AddHomeFolders(string tracerHome, List<string> paths, string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(tracerHome))
            {
                // the home folder could contain the native dll directly (in legacy versions of the package),
                // but it could also be under a runtime specific folder
                AddRuntimeSpecificLocations(paths, tracerHome, runtimeId, includeNative: true);
            }

            // include the appdomain base as this will help framework samples running in IIS find the library
            var currentDir =
                string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ? AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath;

            if (!string.IsNullOrWhiteSpace(currentDir))
            {
                Log.Debug("AppDomain location is {CurrentDir}", currentDir);
                AddRuntimeSpecificLocations(paths, currentDir, runtimeId);
            }
            else
            {
                Log.Debug("AppDomain location is null or empty");
            }
        }

        private static void AddRuntimeSpecificLocations(List<string> paths, string candidatePath, string runtimeId, bool includeNative = false)
        {
            paths.Add(candidatePath);
            paths.Add(Path.Combine(candidatePath, runtimeId));
            if (includeNative)
            {
                paths.Add(Path.Combine(candidatePath, runtimeId, "native"));
            }
        }

        internal static bool TryLoadLibraryFromPaths(string libName, List<string> paths, out IntPtr handle)
        {
            var success = false;
            handle = IntPtr.Zero;

            foreach (var path in paths)
            {
                var libFullPath = Path.Combine(path, libName);

                if (!File.Exists(libFullPath))
                {
                    continue;
                }

                var loaded = NativeLibrary.TryLoad(libFullPath, out handle);

                if (loaded)
                {
                    success = true;
                    Log.Information("Loaded library '{LibName}' from '{Path}' with handle '{Handle}'", libName, path, handle);
                    break;
                }

                Log.Warning("Failed to load library '{LibName}' from '{Path}'", libName, path);
            }

            if (!success)
            {
                Log.Warning("Failed to load library '{LibName}' from any of the following '{Paths}'", libName, string.Join(", ", paths));
                Log.Error("AppSec could not load libddwaf native library, as a result, AppSec could not start. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }

            return success;
        }

        internal static string GetLibName(FrameworkDescription fwk, string libVersion = null)
        {
            string versionSuffix = null;
            if (!string.IsNullOrEmpty(libVersion))
            {
                versionSuffix = $"-{libVersion}";
            }

            return fwk.OSPlatform switch
            {
                OSPlatformName.MacOS => $"libddwaf{versionSuffix}.dylib",
                OSPlatformName.Linux => $"libddwaf{versionSuffix}.so",
                OSPlatformName.Windows => $"ddwaf{versionSuffix}.dll",
                _ => null, // unsupported
            };
        }

        internal static string[] GetRuntimeIds(FrameworkDescription fwk)
            => fwk.OSPlatform switch
            {
                OSPlatformName.MacOS => new[] { $"osx" },
                OSPlatformName.Windows => new[] { $"win-{fwk.ProcessArchitecture}" },
                OSPlatformName.Linux => fwk.ProcessArchitecture == ProcessArchitecture.Arm64
                                            ? new[] { "linux-arm64", "linux-musl-arm64" }
                                            : new[] { "linux-x64", "linux-musl-x64" },
                _ => null, // unsupported
            };
    }
}
