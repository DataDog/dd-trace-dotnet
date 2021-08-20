// <copyright file="Native.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
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

        private static GetVersionDelegate getVersion;
        private static InitHDelegate initH;
        private static ClearRuleHDelegate clearRuleH;
        private static RunHDelegate runH;
        private static FreeReturnDelegate freeReturn;
        private static InitAdditiveHDelegate initAdditiveH;
        private static RunAdditiveDelegate runAdditive;
        private static ClearAdditiveDelegate clearAdditive;
        private static GetInvalidDelegate getInvalid;
        private static CreateStringWithLengthDelegate createStringWithLength;
        private static CreateStringDelegate createString;
        private static CreateIntDelegate createInt;
        private static CreateUintDelegate createUint;
        private static CreateArrayDelegate createArray;
        private static CreateMapDelegate createMap;
        private static AddArrayDelegate addArray;
        private static AddMapDelegate addMap;
        private static FreeArgDelegate freeArg;

        static Native()
        {
            var fd = FrameworkDescription.Create();

            string libName, runtimeId;
            GetLibNameAndRuntimeId(fd, out libName, out runtimeId);

            // libName or runtimeId being null means platform is not supported
            // no point attempting to load the library
            if (libName != null && runtimeId != null)
            {
                var paths = GetDatadogNativeFolders(fd, runtimeId);
                if (TryLoadLibraryFromPaths(libName, paths, out IntPtr handle))
                {
                    getVersion = GetDelegateForNativeFunction<GetVersionDelegate>(handle, "pw_getVersion");
                    initH = GetDelegateForNativeFunction<InitHDelegate>(handle, "pw_initH");
                    clearRuleH = GetDelegateForNativeFunction<ClearRuleHDelegate>(handle, "pw_clearRuleH");
                    runH = GetDelegateForNativeFunction<RunHDelegate>(handle, "pw_runH");
                    freeReturn = GetDelegateForNativeFunction<FreeReturnDelegate>(handle, "pw_freeReturn");
                    initAdditiveH = GetDelegateForNativeFunction<InitAdditiveHDelegate>(handle, "pw_initAdditiveH");
                    runAdditive = GetDelegateForNativeFunction<RunAdditiveDelegate>(handle, "pw_runAdditive");
                    clearAdditive = GetDelegateForNativeFunction<ClearAdditiveDelegate>(handle, "pw_clearAdditive");
                    getInvalid = GetDelegateForNativeFunction<GetInvalidDelegate>(handle, "pw_getInvalid");
                    createStringWithLength = GetDelegateForNativeFunction<CreateStringWithLengthDelegate>(handle, "pw_createStringWithLength");
                    createString = GetDelegateForNativeFunction<CreateStringDelegate>(handle, "pw_createString");
                    createInt = GetDelegateForNativeFunction<CreateIntDelegate>(handle, "pw_createInt");
                    createUint = GetDelegateForNativeFunction<CreateUintDelegate>(handle, "pw_createUint");
                    createArray = GetDelegateForNativeFunction<CreateArrayDelegate>(handle, "pw_createArray");
                    createMap = GetDelegateForNativeFunction<CreateMapDelegate>(handle, "pw_createMap");
                    addArray = GetDelegateForNativeFunction<AddArrayDelegate>(handle, "pw_addArray");
                    addMap = GetDelegateForNativeFunction<AddMapDelegate>(handle, "pw_addMap");
                    freeArg = GetDelegateForNativeFunction<FreeArgDelegate>(handle, "pw_freeArg");
                }
            }
        }

        private delegate PWVersion GetVersionDelegate();

        private delegate IntPtr InitHDelegate(string wafRule, ref PWConfig config, ref string errors);

        private delegate void ClearRuleHDelegate(IntPtr wafHandle);

        private delegate PWRet RunHDelegate(IntPtr wafHandle, PWArgs parameters, ulong timeLeftInUs);

        private delegate void FreeReturnDelegate(PWRet output);

        private delegate IntPtr InitAdditiveHDelegate(IntPtr powerwafHandle);

        private delegate PWRet RunAdditiveDelegate(IntPtr context, PWArgs newArgs, ulong timeLeftInUs);

        private delegate void ClearAdditiveDelegate(IntPtr context);

        private delegate PWArgs GetInvalidDelegate();

        private delegate PWArgs CreateStringWithLengthDelegate(string s, ulong length);

        private delegate PWArgs CreateStringDelegate(string s);

        private delegate PWArgs CreateIntDelegate(long value);

        private delegate PWArgs CreateUintDelegate(ulong value);

        private delegate PWArgs CreateArrayDelegate();

        private delegate PWArgs CreateMapDelegate();

        private delegate bool AddArrayDelegate(ref PWArgs array, PWArgs entry);

        private delegate bool AddMapDelegate(ref PWArgs map, string entryName, ulong entryNameLength, PWArgs entry);

        private delegate void FreeArgDelegate(ref PWArgs input);

#pragma warning disable SA1300 // Element should begin with upper-case letter

        internal static PWVersion pw_getVersion()
        {
            return getVersion();
        }

        internal static IntPtr pw_initH(string wafRule, ref PWConfig config, ref string errors)
        {
            return initH(wafRule, ref config, ref errors);
        }

        internal static void pw_clearRuleH(IntPtr wafHandle)
        {
            clearRuleH(wafHandle);
        }

        internal static PWRet pw_runH(IntPtr wafHandle, PWArgs parameters, ulong timeLeftInUs)
        {
            return runH(wafHandle, parameters, timeLeftInUs);
        }

        internal static void pw_freeReturn(PWRet output)
        {
            freeReturn(output);
        }

        internal static IntPtr pw_initAdditiveH(IntPtr powerwafHandle)
        {
            return initAdditiveH(powerwafHandle);
        }

        internal static PWRet pw_runAdditive(IntPtr context, PWArgs newArgs, ulong timeLeftInUs)
        {
            return runAdditive(context, newArgs, timeLeftInUs);
        }

        internal static void pw_clearAdditive(IntPtr context)
        {
            clearAdditive(context);
        }

        internal static PWArgs pw_getInvalid()
        {
            return getInvalid();
        }

        internal static PWArgs pw_createStringWithLength(string s, ulong length)
        {
            return createStringWithLength(s, length);
        }

        internal static PWArgs pw_createString(string s)
        {
            return createString(s);
        }

        internal static PWArgs pw_createInt(long value)
        {
            return createInt(value);
        }

        internal static PWArgs pw_createUint(ulong value)
        {
            return createUint(value);
        }

        internal static PWArgs pw_createArray()
        {
            return createArray();
        }

        internal static PWArgs pw_createMap()
        {
            return createMap();
        }

        internal static bool pw_addArray(ref PWArgs array, PWArgs entry)
        {
            return addArray(ref array, entry);
        }

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal static bool pw_addMap(ref PWArgs map, string entryName, ulong entryNameLength, PWArgs entry)
        {
            return addMap(ref map, entryName, entryNameLength, entry);
        }

        internal static void pw_freeArg(ref PWArgs input)
        {
            freeArg(ref input);
        }

#pragma warning restore SA1300 // Element should begin with upper-case letter

        private static T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate
        {
            var funcPtr = NativeLibrary.GetExport(handle, functionName);
            Log.Debug("GetDelegateForNativeFunction -  funcPtr: " + funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private static List<string> GetDatadogNativeFolders(FrameworkDescription frameworkDescription, string runtimeId)
        {
            // first get anything "home folder" like, then combine with the profiler's location
            // taking into account that this should be the same place

            var paths = GetHomeFolders(runtimeId);
            var profilerFolder = GetProfilerFolder(frameworkDescription);
            paths.Add(profilerFolder);

            return paths.Distinct().ToList();
        }

        private static string GetProfilerFolder(FrameworkDescription frameworkDescription)
        {
            var profilerEnvVar =
                frameworkDescription.IsCoreClr() ? "CORECLR_PROFILER_PATH" : "COR_PROFILER_PATH";
            var profilerEnvVarBitsExt =
                profilerEnvVar + (Environment.Is64BitProcess ? "_64" : "_32");

            var profilerPathsEnvVars = new List<string>()
            {
                profilerEnvVarBitsExt,
                profilerEnvVar
            };

            // it is unlikely that the security library would be in a sub folder from
            // where the profiler lives, so just use this paths directly
            var profilerFolders =
                profilerPathsEnvVars
                    .Select(Environment.GetEnvironmentVariable)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(Path.GetDirectoryName)
                    .FirstOrDefault();

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

            Log.Debug($"current dir is {currentDir}'");

            // the home folder could contain the native dll directly, but it could also
            // be under a runtime specific folder
            var paths =
                integrationsPaths
                    .Distinct()
                    .SelectMany(GetRuntimeSpecificVersions)
                    .ToList();

            return paths;
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
                else
                {
                    Log.Warning($"Failed to load library '{libName}' from '{path}'");
                }
            }

            if (!success)
            {
                Log.Warning($"Failed to load library '{libName}' from any of the following '{string.Join(", ", paths)}'");
            }

            return success;
        }

        private static bool IsMuslBasedLinux()
        {
            var muslDistros = new[] { "alpine" };

            var files =
                Directory.GetFiles("/etc", "*release")
                    .Concat(Directory.GetFiles("/etc", "*version"))
                    .ToList();

            if (File.Exists("/etc/issue"))
            {
                files.Add("/etc/issue");
            }

            return files
                .Select(File.ReadAllText)
                .Any(fileContents => muslDistros.Any(distroId => fileContents.ToLower(CultureInfo.InvariantCulture).Contains(distroId)));
        }

        private static void GetLibNameAndRuntimeId(FrameworkDescription frameworkDescription, out string libName, out string runtimeId)
        {
            string runtimeIdPart1, libPrefix, libExt;

            switch (frameworkDescription.OSPlatform)
            {
                case OSPlatform.MacOS:
                    runtimeIdPart1 = "osx";
                    libPrefix = "lib";
                    libExt = "dylib";
                    break;
                case OSPlatform.Linux:
                    runtimeIdPart1 =
                        IsMuslBasedLinux() ?
                            "linux-musl" :
                            "linux";
                    libPrefix = "lib";
                    libExt = "so";
                    break;
                case OSPlatform.Windows:
                    runtimeIdPart1 = "win";
                    libPrefix = string.Empty;
                    libExt = "dll";
                    break;
                default:
                    // unsupported platform
                    runtimeIdPart1 = null;
                    libPrefix = null;
                    libExt = null;
                    break;
            }

            if (runtimeIdPart1 != null && libPrefix != null && libExt != null)
            {
                libName = libPrefix + "Sqreen." + libExt;
                runtimeId = Environment.Is64BitProcess ? runtimeIdPart1 + "-x64" : runtimeIdPart1 + "-x86";
            }
            else
            {
                Log.Warning($"Unsupported platform: " + Environment.OSVersion.Platform);

                libName = null;
                runtimeId = null;
            }
        }
    }
}
