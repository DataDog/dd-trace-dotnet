// <copyright file="Native.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class Native
    {
#if NETFRAMEWORK
        private const string DllName = "Sqreen.dll";
#else
        private const string DllName = "Sqreen";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Native));

        static Native()
        {
#if NETFRAMEWORK
            var libName = DllName;
            var runtimeIdPart1 = "win";
#else
            var (runtimeIdPart1, libPrefix, libExt) =
                Environment.OSVersion.Platform switch
                {
                    PlatformID.MacOSX => ("osx", "lib", "dylib"),
                    PlatformID.Unix => ("linux", "lib", "so"),
                    PlatformID.Win32NT => ("win", string.Empty, "dll"),
                    PlatformID.Win32S => ("win", string.Empty, "dll"),
                    PlatformID.Win32Windows => ("win", string.Empty, "dll"),
                    PlatformID.WinCE => ("win", string.Empty, "dll"),
                    PlatformID.Xbox => throw new NotSupportedException(),
                    _ => throw new NotSupportedException(),
                };
            var libName = libPrefix + DllName + "." + libExt;
#endif

            var runtimeId = Environment.Is64BitProcess ? runtimeIdPart1 + "-x64" : runtimeIdPart1 + "-x32";

            var paths = GetDataDogNativeFolders(runtimeId);
            var success = false;

            foreach (var path in paths)
            {
                var libFullPath = Path.Combine(path, libName);

                if (!File.Exists(libFullPath))
                {
                    continue;
                }

                // loading the library is sufficient, once in memory the p/invokes will just work
                var loaded = NativeLibrary.TryLoad(libFullPath, out var _);

                if (loaded)
                {
                    success = true;
                    Log.Information($"Loaded library '{libName}' from '{path}'");
                    break;
                }
                else
                {
                    Log.Warning($"Failed to load library '{libName}' from '{path}'");
                }
            }

            if (!success)
            {
                Log.Warning($"Failed to load library '{libName}' from any of the following '{string.Join(", ", paths)}'");
            }
        }

#pragma warning disable SA1300 // Element should begin with upper-case letter

        [DllImport(DllName)]
        internal static extern PWVersion pw_getVersion();

        [DllImport(DllName)]
        internal static extern IntPtr pw_initH(string wafRule, ref PWConfig config, ref string errors);

        [DllImport(DllName)]
        internal static extern void pw_clearRuleH(IntPtr wafHandle);

        [DllImport(DllName)]
        internal static extern PWRet pw_runH(IntPtr wafHandle, PWArgs parameters, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_freeReturn(PWRet output);

        [DllImport(DllName)]
        internal static extern IntPtr pw_initAdditiveH(IntPtr powerwafHandle);

        [DllImport(DllName)]
        internal static extern PWRet pw_runAdditive(IntPtr context, PWArgs newArgs, ulong timeLeftInUs);

        [DllImport(DllName)]
        internal static extern void pw_clearAdditive(IntPtr context);

        [DllImport(DllName)]
        internal static extern PWArgs pw_getInvalid();

        [DllImport(DllName)]
        internal static extern PWArgs pw_createStringWithLength(string s, ulong length);

        [DllImport(DllName)]
        internal static extern PWArgs pw_createString(string s);

        [DllImport(DllName)]
        internal static extern PWArgs pw_createInt(long value);

        [DllImport(DllName)]
        internal static extern PWArgs pw_createUint(ulong value);

        [DllImport(DllName)]
        internal static extern PWArgs pw_createArray();

        [DllImport(DllName)]
        internal static extern PWArgs pw_createMap();

        [DllImport(DllName)]
        internal static extern bool pw_addArray(ref PWArgs array, PWArgs entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        [DllImport(DllName)]
        internal static extern bool pw_addMap(ref PWArgs map, string entryName, ulong entryNameLength, PWArgs entry);

        [DllImport(DllName)]
        internal static extern void pw_freeArg(ref PWArgs input);

#pragma warning restore SA1300 // Element should begin with upper-case letter

        private static List<string> GetDataDogNativeFolders(string runtimeId)
        {
            // first get anything "home folder" like, then combine with the profiler's location
            // taking into account that this should be the same place

            var homePaths = GetHomeFolders(runtimeId);
            var profilerFolders = GetProfilerFolders();
            var paths = homePaths.Concat(profilerFolders).Distinct().ToList();

            return paths;
        }

        private static IEnumerable<string> GetProfilerFolders()
        {
            var profilerPathsEnvVars = new List<string>()
            {
                "CORECLR_PROFILER_PATH",
                "CORECLR_PROFILER_PATH_32",
                "CORECLR_PROFILER_PATH_64",
                "COR_PROFILER_PATH",
            };

            // it is unlikely that the security library would be in a sub fold from
            // where the profiler lives, so just use this paths directly
            var profilerFolders =
                profilerPathsEnvVars
                    .Select(Environment.GetEnvironmentVariable)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(Path.GetDirectoryName);

            return profilerFolders;
        }

        private static List<string> GetHomeFolders(string runtimeId)
        {
            List<string> GetRuntimeSpecificVersions(string path)
            {
                return new List<string>()
                {
                    path,
                    Path.Combine(path, runtimeId),
                    Path.Combine(path, runtimeId, "native"),
                };
            }

            // treat any path that could contain integrations.json as home folder
            var integrationsPaths = Environment.GetEnvironmentVariable("DD_INTEGRATIONS")
                    ?.Split(';')
                    ?.Where(x => !string.IsNullOrWhiteSpace(x))
                    ?.Select(Path.GetDirectoryName)
                    ?.ToList()
                        ?? new List<string>();

            // the real trace home
            var tracerHome = Environment.GetEnvironmentVariable("DD_DOTNET_TRACER_HOME");
            if (!string.IsNullOrWhiteSpace(tracerHome))
            {
                integrationsPaths.Add(tracerHome);
            }

            // include the appdomain base as this will help framework samples running in IIS find the library
            var currentDir =
                string.IsNullOrEmpty(AppDomain.CurrentDomain.RelativeSearchPath) ?
                    AppDomain.CurrentDomain.BaseDirectory : AppDomain.CurrentDomain.RelativeSearchPath;
            integrationsPaths.Add(currentDir);

            // the home folder could contain the native dll directly, but it could also
            // be under a runtime specific folder
            var paths =
                integrationsPaths
                    .Distinct()
                    .SelectMany(GetRuntimeSpecificVersions)
                    .ToList();

            return paths;
        }
    }
}
