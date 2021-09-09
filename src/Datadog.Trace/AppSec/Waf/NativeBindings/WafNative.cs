// <copyright file="WafNative.cs" company="Datadog">
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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class WafNative
    {
#if NETFRAMEWORK
        private const string DllName = "ddwaf.dll";
#else
        private const string DllName = "ddwaf";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafNative));

        private static readonly GetVersionDelegate GetVersionField;
        private static readonly InitDelegate InitField;
        private static readonly ResultFreeDelegate ResultFreeField;
        private static readonly InitContextDelegate InitContextField;
        private static readonly RunDelegate RunField;
        private static readonly DestroyDelegate DestroyField;
        private static readonly ContextDestroyDelegate ContextDestroyField;
        private static readonly ObjectInvalidDelegate ObjectInvalidField;
        private static readonly ObjectStringLengthDelegateX64 ObjectStringLengthFieldX64;
        private static readonly ObjectStringLengthDelegateX86 ObjectStringLengthFieldX86;
        private static readonly ObjectSignedDelegate ObjectSignedField;
        private static readonly ObjectUnsignedDelegate ObjectUnsignField;
        private static readonly ObjectArrayDelegate ObjectArrayField;
        private static readonly ObjectMapDelegate ObjectMapField;
        private static readonly ObjectArrayAddDelegate ObjectArrayAddField;
        private static readonly ObjectMapAddDelegateX64 ObjectMapAddFieldX64;
        private static readonly ObjectMapAddDelegateX86 ObjectMapAddFieldX86;
        private static readonly ObjectFreeDelegate ObjectFreeField;
        private static readonly SetupLogCallbackDelegate SetupLogCallbackField;
        private static readonly IntPtr ObjectFreeFuncPtrField;

        static WafNative()
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
                    InitField = GetDelegateForNativeFunction<InitDelegate>(handle, "ddwaf_init");
                    DestroyField = GetDelegateForNativeFunction<DestroyDelegate>(handle, "ddwaf_destroy");
                    InitContextField = GetDelegateForNativeFunction<InitContextDelegate>(handle, "ddwaf_context_init");
                    RunField = GetDelegateForNativeFunction<RunDelegate>(handle, "ddwaf_run");
                    ContextDestroyField = GetDelegateForNativeFunction<ContextDestroyDelegate>(handle, "ddwaf_context_destroy");
                    ResultFreeField = GetDelegateForNativeFunction<ResultFreeDelegate>(handle, "ddwaf_result_free");
                    ObjectInvalidField = GetDelegateForNativeFunction<ObjectInvalidDelegate>(handle, "ddwaf_object_invalid");
                    ObjectStringLengthFieldX64 =
                        Environment.Is64BitProcess ?
                            GetDelegateForNativeFunction<ObjectStringLengthDelegateX64>(handle, "ddwaf_object_stringl") :
                            null;
                    ObjectStringLengthFieldX86 =
                        Environment.Is64BitProcess ?
                            null :
                            GetDelegateForNativeFunction<ObjectStringLengthDelegateX86>(handle, "ddwaf_object_stringl");
                    ObjectSignedField = GetDelegateForNativeFunction<ObjectSignedDelegate>(handle, "ddwaf_object_signed");
                    ObjectUnsignField = GetDelegateForNativeFunction<ObjectUnsignedDelegate>(handle, "ddwaf_object_unsigned");
                    ObjectArrayField = GetDelegateForNativeFunction<ObjectArrayDelegate>(handle, "ddwaf_object_array");
                    ObjectMapField = GetDelegateForNativeFunction<ObjectMapDelegate>(handle, "ddwaf_object_map");
                    ObjectArrayAddField = GetDelegateForNativeFunction<ObjectArrayAddDelegate>(handle, "ddwaf_object_array_add");
                    ObjectMapAddFieldX64 =
                        Environment.Is64BitProcess ?
                            GetDelegateForNativeFunction<ObjectMapAddDelegateX64>(handle, "ddwaf_object_map_addl") :
                            null;
                    ObjectMapAddFieldX86 =
                        Environment.Is64BitProcess ?
                            null :
                            GetDelegateForNativeFunction<ObjectMapAddDelegateX86>(handle, "ddwaf_object_map_addl");
                    ObjectFreeField = GetDelegateForNativeFunction<ObjectFreeDelegate>(handle, "ddwaf_object_free", out ObjectFreeFuncPtrField);
                    GetVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(handle, "ddwaf_get_version");

                    // setup logging
                    var setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(handle, "ddwaf_set_log_cb");
                    // convert to a delegate and attempt to pin it by assigning it to static field
                    SetupLogCallbackField = LoggingCallback;
                    // set the log level and setup the loger
                    var level = GlobalSettings.Source.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
                    setupLogging(Marshal.GetFunctionPointerForDelegate(SetupLogCallbackField), level);
                }
            }
        }

        private delegate DdwafVersionStruct GetVersionDelegate();

        private delegate IntPtr InitDelegate(IntPtr wafRule, ref DdwafConfigStruct config);

        private delegate void ResultFreeDelegate(ref DdwafResultStruct output);

        private delegate IntPtr InitContextDelegate(IntPtr powerwafHandle, IntPtr objFree);

        private delegate DDWAF_RET_CODE RunDelegate(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs);

        private delegate void DestroyDelegate(IntPtr handle);

        private delegate void ContextDestroyDelegate(IntPtr context);

        private delegate IntPtr ObjectInvalidDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectStringLengthDelegateX64(IntPtr emptyObjPtr, string s, ulong length);

        private delegate IntPtr ObjectStringLengthDelegateX86(IntPtr emptyObjPtr, string s, uint length);

        private delegate IntPtr ObjectSignedDelegate(IntPtr emptyObjPtr, long value);

        private delegate IntPtr ObjectUnsignedDelegate(IntPtr emptyObjPtr, ulong value);

        private delegate IntPtr ObjectArrayDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectMapDelegate(IntPtr emptyObjPtr);

        private delegate bool ObjectArrayAddDelegate(IntPtr array, IntPtr entry);

        private delegate bool ObjectMapAddDelegateX64(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry);

        private delegate bool ObjectMapAddDelegateX86(IntPtr map, string entryName, uint entryNameLength, IntPtr entry);

        private delegate void ObjectFreeDelegate(IntPtr input);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetupLogCallbackDelegate(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            int line,
            string message,
            ulong message_len);

        private delegate bool SetupLoggingDelegate(IntPtr cb, DDWAF_LOG_LEVEL min_level);

        private enum DDWAF_LOG_LEVEL
        {
            DDWAF_TRACE,
            DDWAF_DEBUG,
            DDWAF_INFO,
            DDWAF_WARN,
            DDWAF_ERROR,
            DDWAF_AFTER_LAST,
        }

        internal static IntPtr ObjectFreeFuncPtr
        {
            get { return ObjectFreeFuncPtrField; }
        }

        internal static DdwafVersionStruct GetVersion()
        {
            return GetVersionField();
        }

        internal static IntPtr Init(IntPtr wafRule, ref DdwafConfigStruct config)
        {
            return InitField(wafRule, ref config);
        }

        internal static void ResultFree(ref DdwafResultStruct output)
        {
            ResultFreeField(ref output);
        }

        internal static IntPtr InitContext(IntPtr powerwafHandle, IntPtr objFree)
        {
            return InitContextField(powerwafHandle, objFree);
        }

        internal static DDWAF_RET_CODE Run(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs)
        {
            return RunField(context, newArgs, ref result, timeLeftInUs);
        }

        internal static void Destroy(IntPtr handle)
        {
            DestroyField(handle);
        }

        internal static void ContextDestroy(IntPtr handle)
        {
            ContextDestroyField(handle);
        }

        internal static IntPtr ObjectInvalid()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            ObjectInvalidField(ptr);
            return ptr;
        }

        internal static IntPtr ObjectStringLength(string s, ulong length)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            if (Environment.Is64BitProcess)
            {
                ObjectStringLengthFieldX64(ptr, s, length);
            }
            else
            {
                ObjectStringLengthFieldX86(ptr, s, (uint)length);
            }

            return ptr;
        }

        internal static IntPtr ObjectSigned(long value)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            ObjectSignedField(ptr, value);
            return ptr;
        }

        internal static IntPtr ObjectUnsigned(ulong value)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            ObjectUnsignField(ptr, value);
            return ptr;
        }

        internal static IntPtr ObjectArray()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            ObjectArrayField(ptr);
            return ptr;
        }

        internal static IntPtr ObjectMap()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            ObjectMapField(ptr);
            return ptr;
        }

        internal static bool ObjectArrayAdd(IntPtr array, IntPtr entry)
        {
            return ObjectArrayAddField(array, entry);
        }

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal static bool ObjectMapAdd(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry)
        {
            return
                Environment.Is64BitProcess ?
                    ObjectMapAddFieldX64(map, entryName, entryNameLength, entry) :
                    ObjectMapAddFieldX86(map, entryName, (uint)entryNameLength, entry);
        }

        internal static void ObjectFree(IntPtr input)
        {
            ObjectFreeField(input);
        }

        private static void LoggingCallback(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            int line,
            string message,
            ulong message_len)
        {
            var formattedMessage = $"{level}: [{function}]{file}({line}): {message}";
            switch (level)
            {
                case DDWAF_LOG_LEVEL.DDWAF_TRACE:
                case DDWAF_LOG_LEVEL.DDWAF_DEBUG:
                    Log.Debug(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_INFO:
                    Log.Information(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_WARN:
                    Log.Warning(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_ERROR:
                case DDWAF_LOG_LEVEL.DDWAF_AFTER_LAST:
                    Log.Error(formattedMessage);
                    break;
                default:
                    Log.Error("[Unknown level] " + formattedMessage);
                    break;
            }
        }

        private static T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
            where T : Delegate
        {
            funcPtr = NativeLibrary.GetExport(handle, functionName);
            Log.Debug("GetDelegateForNativeFunction -  funcPtr: " + funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private static T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate
        {
            return GetDelegateForNativeFunction<T>(handle, functionName, out var _);
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
                .Any(fileContents => muslDistros.Any(distroId => fileContents.ToLowerInvariant().Contains(distroId)));
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
                libName = libPrefix + "ddwaf." + libExt;
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
