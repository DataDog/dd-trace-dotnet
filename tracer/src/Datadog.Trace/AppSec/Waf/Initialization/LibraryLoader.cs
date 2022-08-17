// <copyright file="LibraryLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal static class LibraryLoader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LibraryLoader));

        internal static IntPtr LoadAndGetHandle()
        {
            var fd = FrameworkDescription.Instance;

            var libName = GetLibName(fd);
            var runtimeIds = GetRuntimeIds(fd);

            // libName or runtimeIds being null means platform is not supported
            // no point attempting to load the library
            if (libName != null && runtimeIds != null)
            {
                var paths = GetDatadogNativeFolders(fd, runtimeIds);
                if (TryLoadLibraryFromPaths(libName, paths, out var handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }

        private static List<string> GetDatadogNativeFolders(FrameworkDescription frameworkDescription, string[] runtimeIds)
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

            AddNativeLoaderEnginePath(paths);

            foreach (var runtimeId in runtimeIds)
            {
                AddHomeFolders(paths, runtimeId);
            }

            foreach (var runtimeId in runtimeIds)
            {
                AddProfilerFolders(paths, frameworkDescription, runtimeId);
            }

            return paths.Distinct().ToList();
        }

        private static void AddNativeLoaderEnginePath(List<string> paths)
        {
            // The native loader sets this env var to say where it's loaded from, so the waf should be next to it
            // Use this preferentially over other options
             var value = EnvironmentHelpers.GetEnvironmentVariable("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH");
             if (!string.IsNullOrWhiteSpace(value))
             {
                 paths.Add(Path.GetDirectoryName(value));
             }
        }

        private static void AddProfilerFolders(List<string> paths, FrameworkDescription frameworkDescription, string runtimeId)
        {
            var profilerEnvVar =
                frameworkDescription.IsCoreClr() ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH";

            if (TryAddProfilerFolders(paths, profilerEnvVar))
            {
                // added the locations
                return;
            }

            var profilerEnvVarBitsExt =
                profilerEnvVar + (Environment.Is64BitProcess ? "_64" : "_32");

            if (!TryAddProfilerFolders(paths, profilerEnvVarBitsExt))
            {
                // this is expected under Windows, but problematic under other OSs
                Log.Debug("Couldn't find profilerFolder");
            }

            bool TryAddProfilerFolders(List<string> pathLists, string envVar)
            {
                var value = EnvironmentHelpers.GetEnvironmentVariable(envVar);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                AddRuntimeSpecificLocations(pathLists, Path.GetDirectoryName(value), runtimeId);
                return true;
            }
        }

        private static void AddHomeFolders(List<string> paths, string runtimeId)
        {
            // the real trace home
            var tracerHome = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
            if (!string.IsNullOrWhiteSpace(tracerHome))
            {
                // the home folder could contain the native dll directly (in legacy versions of the package),
                // but it could also be under a runtime specific folder
                AddRuntimeSpecificLocations(paths, tracerHome, runtimeId, includeNative: true);
            }

            // include the appdomain base as this will help framework samples running in IIS find the library
            var currentDir =
                string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ?
                    AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath;

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

        private static bool TryLoadLibraryFromPaths(string libName, List<string> paths, out IntPtr handle)
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
                    Log.Information($"Loaded library '{libName}' from '{path}' with handle '{handle}'");
                    break;
                }

                Log.Warning($"Failed to load library '{libName}' from '{path}'");
            }

            if (!success)
            {
                Log.Warning($"Failed to load library '{libName}' from any of the following '{string.Join(", ", paths)}'");
                Log.Error("AppSec could not load libddwaf native library, as a result, AppSec could not start. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }

            return success;
        }

        private static string GetLibName(FrameworkDescription fwk)
            => fwk.OSPlatform switch
            {
                OSPlatformName.MacOS => "libddwaf.dylib",
                OSPlatformName.Linux => "libddwaf.so",
                OSPlatformName.Windows => "ddwaf.dll",
                _ => null, // unsupported
            };

        private static string[] GetRuntimeIds(FrameworkDescription fwk)
            => fwk.OSPlatform switch
            {
                OSPlatformName.MacOS => new[] { $"osx-x64" },
                OSPlatformName.Windows => new[] { $"win-{fwk.ProcessArchitecture}" },
                OSPlatformName.Linux => fwk.ProcessArchitecture == ProcessArchitecture.Arm64
                                            ? new[] { "linux-arm64" }
                                            : new[] { "linux-x64", "linux-musl-x64" },
                _ => null, // unsupported
            };
    }
}
