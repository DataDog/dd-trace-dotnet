// <copyright file="WafNative.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class WafNative
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafNative));
        private static GetVersionDelegate _getVersionField;
        private static InitDelegate _initField;
        private static ResultFreeDelegate _resultFreeField;
        private static InitContextDelegate _initContextField;
        private static RunDelegate _runField;
        private static DestroyDelegate _destroyField;
        private static ContextDestroyDelegate _contextDestroyField;
        private static ObjectInvalidDelegate _objectInvalidField;
        private static ObjectStringLengthDelegateX64 _objectStringLengthFieldX64;
        private static ObjectStringLengthDelegateX86 _objectStringLengthFieldX86;
        private static ObjectSignedDelegate _objectSignedField;
        private static ObjectUnsignedDelegate _objectUnsignField;
        private static ObjectArrayDelegate _objectArrayField;
        private static ObjectMapDelegate _objectMapField;
        private static ObjectArrayAddDelegate _objectArrayAddField;
        private static ObjectMapAddDelegateX64 _objectMapAddFieldX64;
        private static ObjectMapAddDelegateX86 _objectMapAddFieldX86;
        private static ObjectFreeDelegate _objectFreeField;
        private static SetupLogCallbackDelegate _setupLogCallbackField;
        private static IntPtr _objectFreeFuncPtrField;
        private static WafNative _instance;

        private WafNative()
        {
        }

        private delegate void GetVersionDelegate(ref DdwafVersionStruct version);

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

        internal IntPtr ObjectFreeFuncPtr => _objectFreeFuncPtrField;

        public static WafNative Instance => LazyInitializer.EnsureInitialized(ref _instance, Initialize);

        private static WafNative Initialize()
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
                    _initField = GetDelegateForNativeFunction<InitDelegate>(handle, "ddwaf_init");
                    _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(handle, "ddwaf_destroy");
                    _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(handle, "ddwaf_context_init");
                    _runField = GetDelegateForNativeFunction<RunDelegate>(handle, "ddwaf_run");
                    _contextDestroyField = GetDelegateForNativeFunction<ContextDestroyDelegate>(handle, "ddwaf_context_destroy");
                    _resultFreeField = GetDelegateForNativeFunction<ResultFreeDelegate>(handle, "ddwaf_result_free");
                    _objectInvalidField = GetDelegateForNativeFunction<ObjectInvalidDelegate>(handle, "ddwaf_object_invalid");
                    _objectStringLengthFieldX64 =
                        Environment.Is64BitProcess ?
                            GetDelegateForNativeFunction<ObjectStringLengthDelegateX64>(handle, "ddwaf_object_stringl") :
                            null;
                    _objectStringLengthFieldX86 =
                        Environment.Is64BitProcess ?
                            null :
                            GetDelegateForNativeFunction<ObjectStringLengthDelegateX86>(handle, "ddwaf_object_stringl");
                    _objectSignedField = GetDelegateForNativeFunction<ObjectSignedDelegate>(handle, "ddwaf_object_signed");
                    _objectUnsignField = GetDelegateForNativeFunction<ObjectUnsignedDelegate>(handle, "ddwaf_object_unsigned");
                    _objectArrayField = GetDelegateForNativeFunction<ObjectArrayDelegate>(handle, "ddwaf_object_array");
                    _objectMapField = GetDelegateForNativeFunction<ObjectMapDelegate>(handle, "ddwaf_object_map");
                    _objectArrayAddField = GetDelegateForNativeFunction<ObjectArrayAddDelegate>(handle, "ddwaf_object_array_add");
                    _objectMapAddFieldX64 =
                        Environment.Is64BitProcess ?
                            GetDelegateForNativeFunction<ObjectMapAddDelegateX64>(handle, "ddwaf_object_map_addl") :
                            null;
                    _objectMapAddFieldX86 =
                        Environment.Is64BitProcess ?
                            null :
                            GetDelegateForNativeFunction<ObjectMapAddDelegateX86>(handle, "ddwaf_object_map_addl");
                    _objectFreeField = GetDelegateForNativeFunction<ObjectFreeDelegate>(handle, "ddwaf_object_free", out _objectFreeFuncPtrField);
                    _getVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(handle, "ddwaf_get_version");

                    // setup logging
                    var setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(handle, "ddwaf_set_log_cb");
                    // convert to a delegate and attempt to pin it by assigning it to static field
                    _setupLogCallbackField = LoggingCallback;
                    // set the log level and setup the loger
                    var level = GlobalSettings.Source.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
                    setupLogging(Marshal.GetFunctionPointerForDelegate(_setupLogCallbackField), level);
                    return new WafNative();
                }
            }

            return null;
        }

        internal DdwafVersionStruct GetVersion()
        {
            DdwafVersionStruct version = default;
            _getVersionField(ref version);
            return version;
        }

        internal IntPtr Init(IntPtr wafRule, ref DdwafConfigStruct config)
        {
            return _initField(wafRule, ref config);
        }

        internal void ResultFree(ref DdwafResultStruct output)
        {
            _resultFreeField(ref output);
        }

        internal IntPtr InitContext(IntPtr powerwafHandle, IntPtr objFree)
        {
            return _initContextField(powerwafHandle, objFree);
        }

        internal DDWAF_RET_CODE Run(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs)
        {
            return _runField(context, newArgs, ref result, timeLeftInUs);
        }

        internal void Destroy(IntPtr handle)
        {
            _destroyField(handle);
        }

        internal void ContextDestroy(IntPtr handle)
        {
            _contextDestroyField(handle);
        }

        internal IntPtr ObjectInvalid()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectInvalidField(ptr);
            return ptr;
        }

        internal IntPtr ObjectStringLength(string s, ulong length)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            if (Environment.Is64BitProcess)
            {
                _objectStringLengthFieldX64(ptr, s, length);
            }
            else
            {
                _objectStringLengthFieldX86(ptr, s, (uint)length);
            }

            return ptr;
        }

        internal IntPtr ObjectSigned(long value)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectSignedField(ptr, value);
            return ptr;
        }

        internal IntPtr ObjectUnsigned(ulong value)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectUnsignField(ptr, value);
            return ptr;
        }

        internal IntPtr ObjectArray()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectArrayField(ptr);
            return ptr;
        }

        internal IntPtr ObjectMap()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectMapField(ptr);
            return ptr;
        }

        internal bool ObjectArrayAdd(IntPtr array, IntPtr entry)
        {
            return _objectArrayAddField(array, entry);
        }

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal bool ObjectMapAdd(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry)
        {
            return
                Environment.Is64BitProcess ?
                    _objectMapAddFieldX64(map, entryName, entryNameLength, entry) :
                    _objectMapAddFieldX86(map, entryName, (uint)entryNameLength, entry);
        }

        internal void ObjectFree(IntPtr input)
        {
            _objectFreeField(input);
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
                profilerEnvVarBitsExt, profilerEnvVar
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
                    path, Path.Combine(path, runtimeId), Path.Combine(path, runtimeId, "native"),
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
            var muslDistros = new[]
            {
                "alpine"
            };

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

 #pragma warning disable SA1201
        private enum DDWAF_LOG_LEVEL
 #pragma warning restore SA1201
        {
            DDWAF_TRACE,
            DDWAF_DEBUG,
            DDWAF_INFO,
            DDWAF_WARN,
            DDWAF_ERROR,
            DDWAF_AFTER_LAST,
        }
    }
}
