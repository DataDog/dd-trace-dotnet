// <copyright file="WafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Logging;

#pragma warning disable SA1401

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal sealed class WafLibraryInvoker : IWafLibraryInvoker
    {
#if NETFRAMEWORK
        private const string DllName = "ddwaf.dll";
#else
        private const string DllName = "ddwaf";
#endif
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafLibraryInvoker));
        private readonly GetVersionDelegate _getVersionField;

        private readonly BuilderInitDelegate _builderInitField;
        private readonly BuilderAddOrUpdateConfigDelegate _builderAddOrUpdateConfigField;
        private readonly BuilderRemoveConfigDelegate _builderRemoveConfigDelegate;
        private readonly BuilderBuildInstanceDelegate _builderBuildInstanceDelegate;

        private readonly InitContextDelegate _initContextField;
        private readonly RunDelegate _runField;
        private readonly DestroyDelegate _destroyField;
        private readonly ContextDestroyDelegate _contextDestroyField;
        private readonly ObjectInvalidDelegate _objectInvalidField;
        private readonly ObjectStringLengthDelegate _objectStringLengthField;
        private readonly ObjectBoolDelegate _objectBoolField;
        private readonly ObjectUlongDelegate _objectUlongField;
        private readonly ObjectLongDelegate _objectLongField;
        private readonly ObjectDoubleDelegate _objectDoubleField;
        private readonly ObjectNullDelegate _objectNullField;
        private readonly ObjectArrayDelegate _objectArrayField;
        private readonly ObjectMapDelegate _objectMapField;
        private readonly ObjectArrayAddDelegate _objectArrayAddField;
        private readonly ObjectArrayGetAtIndexDelegate _objectArrayGetIndex;
        private readonly ObjectMapAddDelegateX64 _objectMapAddFieldX64;
        private readonly ObjectMapAddDelegateX86 _objectMapAddFieldX86;
        private readonly FreeObjectDelegate _freeObjectField;
        private readonly SetupLoggingDelegate _setupLogging;
        private readonly SetupLogCallbackDelegate _setupLogCallbackField;
        private readonly GetKnownAddressesDelegate _getKnownAddresses;
        private string _version;
        private bool _isKnownAddressesSuported;

        private WafLibraryInvoker(IntPtr libraryHandle, string libVersion = null)
        {
            ExportErrorHappened = false;

            _builderInitField = GetDelegateForNativeFunction<BuilderInitDelegate>(libraryHandle, "ddwaf_builder_init");
            _builderAddOrUpdateConfigField = GetDelegateForNativeFunction<BuilderAddOrUpdateConfigDelegate>(libraryHandle, "ddwaf_builder_add_or_update_config");
            _builderRemoveConfigDelegate = GetDelegateForNativeFunction<BuilderRemoveConfigDelegate>(libraryHandle, "ddwaf_builder_remove_config");
            _builderBuildInstanceDelegate = GetDelegateForNativeFunction<BuilderBuildInstanceDelegate>(libraryHandle, "ddwaf_builder_build_instance");

            _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(libraryHandle, "ddwaf_context_init");
            _runField = GetDelegateForNativeFunction<RunDelegate>(libraryHandle, "ddwaf_run");
            _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(libraryHandle, "ddwaf_destroy");
            _contextDestroyField = GetDelegateForNativeFunction<ContextDestroyDelegate>(libraryHandle, "ddwaf_context_destroy");
            _objectInvalidField = GetDelegateForNativeFunction<ObjectInvalidDelegate>(libraryHandle, "ddwaf_object_invalid");
            _objectStringLengthField = GetDelegateForNativeFunction<ObjectStringLengthDelegate>(libraryHandle, "ddwaf_object_stringl");
            _objectBoolField = GetDelegateForNativeFunction<ObjectBoolDelegate>(libraryHandle, "ddwaf_object_bool");
            _objectLongField = GetDelegateForNativeFunction<ObjectLongDelegate>(libraryHandle, "ddwaf_object_signed");
            _objectUlongField = GetDelegateForNativeFunction<ObjectUlongDelegate>(libraryHandle, "ddwaf_object_unsigned");
            _objectDoubleField = GetDelegateForNativeFunction<ObjectDoubleDelegate>(libraryHandle, "ddwaf_object_float");
            _objectNullField = GetDelegateForNativeFunction<ObjectNullDelegate>(libraryHandle, "ddwaf_object_null");
            _objectArrayField = GetDelegateForNativeFunction<ObjectArrayDelegate>(libraryHandle, "ddwaf_object_array");
            _objectMapField = GetDelegateForNativeFunction<ObjectMapDelegate>(libraryHandle, "ddwaf_object_map");
            _objectArrayAddField = GetDelegateForNativeFunction<ObjectArrayAddDelegate>(libraryHandle, "ddwaf_object_array_add");
            _objectArrayGetIndex = GetDelegateForNativeFunction<ObjectArrayGetAtIndexDelegate>(libraryHandle, "ddwaf_object_get_index");
            _objectMapAddFieldX64 =
                Environment.Is64BitProcess ? GetDelegateForNativeFunction<ObjectMapAddDelegateX64>(libraryHandle, "ddwaf_object_map_addl") : null;
            _objectMapAddFieldX86 =
                Environment.Is64BitProcess ? null : GetDelegateForNativeFunction<ObjectMapAddDelegateX86>(libraryHandle, "ddwaf_object_map_addl");
            _freeObjectField = GetDelegateForNativeFunction<FreeObjectDelegate>(libraryHandle, "ddwaf_object_free");
            _getVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(libraryHandle, "ddwaf_get_version");
            // setup logging
            _setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(libraryHandle, "ddwaf_set_log_cb");
            // Get know addresses
            if (IsKnowAddressesSuported(libVersion))
            {
                _getKnownAddresses = GetDelegateForNativeFunction<GetKnownAddressesDelegate>(libraryHandle, "ddwaf_known_addresses");
            }

            // convert to a delegate and attempt to pin it by assigning it to  field
            _setupLogCallbackField = new SetupLogCallbackDelegate(LoggingCallback);
        }

        private delegate IntPtr GetVersionDelegate();

        private delegate IntPtr BuilderInitDelegate(ref DdwafConfigStruct config);

        private delegate bool BuilderAddOrUpdateConfigDelegate(IntPtr builder, string path, uint pathLen, ref DdwafObjectStruct config, ref DdwafObjectStruct diagnostics);

        private delegate bool BuilderRemoveConfigDelegate(IntPtr builder, string path, uint pathLen);

        private delegate IntPtr BuilderBuildInstanceDelegate(IntPtr builder);

        private delegate IntPtr InitContextDelegate(IntPtr wafHandle);

        private unsafe delegate WafReturnCode RunDelegate(IntPtr context, DdwafObjectStruct* rawPersistentData, DdwafObjectStruct* rawEphemeralData, ref DdwafObjectStruct result, ulong timeLeftInUs);

        private delegate void DestroyDelegate(IntPtr handle);

        private delegate void ContextDestroyDelegate(IntPtr context);

        private delegate IntPtr ObjectInvalidDelegate(ref DdwafObjectStruct emptyObjPtr);

        private delegate IntPtr ObjectStringLengthDelegate(ref DdwafObjectStruct emptyObjPtr, string s, ulong length);

        private delegate IntPtr ObjectBoolDelegate(ref DdwafObjectStruct emptyObjPtr, bool b);

        private delegate IntPtr ObjectDoubleDelegate(ref DdwafObjectStruct emptyObjPtr, double value);

        private delegate IntPtr ObjectNullDelegate(ref DdwafObjectStruct emptyObjPtr);

        private delegate IntPtr ObjectUlongDelegate(ref DdwafObjectStruct emptyObjPtr, ulong value);

        private delegate IntPtr ObjectLongDelegate(ref DdwafObjectStruct emptyObjPtr, long value);

        private delegate IntPtr ObjectArrayDelegate(ref DdwafObjectStruct emptyObjPtr);

        private delegate IntPtr ObjectMapDelegate(ref DdwafObjectStruct emptyObjPtr);

        private delegate bool ObjectArrayAddDelegate(ref DdwafObjectStruct array, ref DdwafObjectStruct entry);

        private delegate IntPtr ObjectArrayGetAtIndexDelegate(ref DdwafObjectStruct array, long index);

        private delegate bool ObjectMapAddDelegateX64(ref DdwafObjectStruct map, string entryName, ulong entryNameLength, ref DdwafObjectStruct entry);

        private delegate bool ObjectMapAddDelegateX86(ref DdwafObjectStruct map, string entryName, uint entryNameLength, ref DdwafObjectStruct entry);

        private delegate void FreeObjectDelegate(ref DdwafObjectStruct input);

        private delegate IntPtr GetKnownAddressesDelegate(IntPtr wafHandle, ref uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetupLogCallbackDelegate(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            uint line,
            string message,
            ulong message_len);

        private delegate bool SetupLoggingDelegate(SetupLogCallbackDelegate cb, DDWAF_LOG_LEVEL min_level);

        private enum DDWAF_LOG_LEVEL
        {
            DDWAF_TRACE,
            DDWAF_DEBUG,
            DDWAF_INFO,
            DDWAF_WARN,
            DDWAF_ERROR,
            DDWAF_AFTER_LAST,
        }

        internal bool ExportErrorHappened { get; private set; }

        /// <summary>
        /// Initializes static members of the <see cref="WafLibraryInvoker"/> class.
        /// </summary>
        /// <param name="ddDotnetTracerHome">DD_DOTNET_TRACER_HOME value</param>
        /// <param name="traceNativeEnginePath">DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH value, internal env var set by native loader</param>
        /// <param name="libVersion">can be null, means use a specific version in the name of the loaded file </param>
        internal static LibraryInitializationResult Initialize(string ddDotnetTracerHome, string traceNativeEnginePath, string libVersion = null)
        {
            var fd = FrameworkDescription.Instance;

            var libName = LibraryLocationHelper.GetLibName(fd, libVersion);
            var runtimeIds = LibraryLocationHelper.GetRuntimeIds(fd);

            // libName or runtimeIds being null means platform is not supported
            // no point attempting to load the library
            IntPtr libraryHandle;
            if (libName != null && runtimeIds != null)
            {
                var paths = LibraryLocationHelper.GetDatadogNativeFolders(ddDotnetTracerHome, traceNativeEnginePath, fd, runtimeIds);
                if (!LibraryLocationHelper.TryLoadLibraryFromPaths(libName, paths, out libraryHandle))
                {
                    return new LibraryInitializationResult(LibraryInitializationResult.LoadStatus.LibraryLoad);
                }
            }
            else
            {
                Log.Error("Lib name or runtime ids is null, current platform {Fd} is likely not supported", fd.ToString());
                return new LibraryInitializationResult(LibraryInitializationResult.LoadStatus.PlatformNotSupported);
            }

            var wafLibraryInvoker = new WafLibraryInvoker(libraryHandle, libVersion);
            if (wafLibraryInvoker.ExportErrorHappened)
            {
                Log.Error("Waf library couldn't initialize properly because of missing methods in native library, please make sure the tracer has been correctly installed and that previous versions are correctly uninstalled.");
                NativeLibrary.CloseLibrary(libraryHandle);
                return new LibraryInitializationResult(LibraryInitializationResult.LoadStatus.ExportError);
            }

            var isCompatible = CheckVersionCompatibility(wafLibraryInvoker);
            if (!isCompatible)
            {
                // no log because CheckVersionCompatibility writes logs in error cases
                NativeLibrary.CloseLibrary(libraryHandle);
                return new LibraryInitializationResult(LibraryInitializationResult.LoadStatus.VersionNotCompatible);
            }

            return new LibraryInitializationResult(wafLibraryInvoker);
        }

        private static bool CheckVersionCompatibility(WafLibraryInvoker wafLibraryInvoker)
        {
            var versionWaf = wafLibraryInvoker.GetVersion();
            var versionWafSplit = versionWaf.Split('.');
            if (versionWafSplit.Length != 3)
            {
                Log.Warning("Waf version {WafVersion} has a non expected format", versionWaf);
                return false;
            }

            var canParse = int.TryParse(versionWafSplit[1], out var wafMinor);
            canParse &= int.TryParse(versionWafSplit[0], out var wafMajor);
            var tracerVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (tracerVersion is null || !canParse)
            {
                Log.Warning("Waf version {WafVersion} or tracer version {TracerVersion} have a non expected format", versionWaf, tracerVersion);
                return false;
            }

            // Waf version 1.25 or higher needed
            if (wafMajor < 1 || (wafMajor == 1 && wafMinor < 25))
            {
                Log.Warning("Waf version {WafVersion} is not compatible with tracer version {TracerVersion}", versionWaf, tracerVersion);
                return false;
            }

            return true;
        }

        internal void SetupLogging(bool wafDebugEnabled)
        {
            var logLevel = wafDebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            _setupLogging(_setupLogCallbackField, logLevel);
        }

        internal string[] GetKnownAddresses(IntPtr wafHandle)
        {
            try
            {
                if (_isKnownAddressesSuported)
                {
                    uint size = 0;
                    var result = _getKnownAddresses(wafHandle, ref size);

                    if (size > 0)
                    {
                        string[] knownAddresses = new string[size];

                        for (uint i = 0; i < size; i++)
                        {
                            // Calculate the pointer to each string
                            var stringPtr = Marshal.ReadIntPtr(result, (int)i * IntPtr.Size);
                            knownAddresses[i] = Marshal.PtrToStringAnsi(stringPtr);
                        }

                        return knownAddresses;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error getting known addresses");
                _isKnownAddressesSuported = false;
            }

            return Array.Empty<string>();
        }

        internal bool IsKnowAddressesSuported(string libVersion = null)
        {
            try
            {
                if (_version is null && libVersion is not null)
                {
                    _version = libVersion;
                }

                if (_version is null)
                {
                    GetVersion();
                    _isKnownAddressesSuported = !string.IsNullOrEmpty(_version) && new Version(_version) >= new Version("1.19.0");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while checking if known addresses are supported");
                _isKnownAddressesSuported = false;
            }

            return _isKnownAddressesSuported;
        }

        internal string GetVersion()
        {
            if (_version == null)
            {
                var ptr = _getVersionField();
                _version = Marshal.PtrToStringAnsi(ptr);
            }

            return _version;
        }

        internal IntPtr InitBuilder(ref DdwafConfigStruct config) => _builderInitField(ref config);

        internal bool BuilderAddOrUpdateConfig(IntPtr builder, string path, ref DdwafObjectStruct config, ref DdwafObjectStruct diagnostics) => _builderAddOrUpdateConfigField(builder, path, (uint)path.Length, ref config, ref diagnostics);

        internal bool BuilderRemoveConfig(IntPtr builder, string path) => _builderRemoveConfigDelegate(builder, path, (uint)path.Length);

        internal IntPtr BuilderBuildInstance(IntPtr builder) => _builderBuildInstanceDelegate(builder);

        internal IntPtr InitContext(IntPtr powerwafHandle) => _initContextField(powerwafHandle);

        /// <summary>
        /// WARNING: do not dispose newArgs until the Context is discarded as well
        /// </summary>
        /// <param name="context">waf context, can sustain multiple runs, args are cached</param>
        /// <param name="rawPersistentData">these pointers SHOULD remain alive until the context is disposed</param>
        /// <param name="rawEphemeralData">these pointers are not cached so can be disposed</param>
        /// <param name="result">Result</param>
        /// <param name="timeLeftInUs">timeout</param>
        /// <returns>Return waf code</returns>
        internal unsafe WafReturnCode Run(IntPtr context, DdwafObjectStruct* rawPersistentData, DdwafObjectStruct* rawEphemeralData, ref DdwafObjectStruct result, ulong timeLeftInUs)
            => _runField(context, rawPersistentData, rawEphemeralData, ref result, timeLeftInUs);

        internal void Destroy(IntPtr wafHandle) => _destroyField(wafHandle);

        public void ContextDestroy(IntPtr handle) => _contextDestroyField(handle);

        public void ObjectFree(ref DdwafObjectStruct input) => _freeObjectField(ref input);

        internal IntPtr ObjectArrayGetIndex(ref DdwafObjectStruct array, long index) => _objectArrayGetIndex(ref array, index);

        internal DdwafObjectStruct ObjectInvalid()
        {
            var item = new DdwafObjectStruct();
            _objectInvalidField(ref item);
            return item;
        }

        internal DdwafObjectStruct ObjectStringLength(string s, ulong length)
        {
            var item = new DdwafObjectStruct();
            _objectStringLengthField(ref item, s, length);
            return item;
        }

        internal DdwafObjectStruct ObjectBool(bool b)
        {
            var item = new DdwafObjectStruct();
            _objectBoolField(ref item, b);
            return item;
        }

        internal DdwafObjectStruct ObjectLong(long l)
        {
            var item = new DdwafObjectStruct();
            _objectLongField(ref item, l);
            return item;
        }

        internal DdwafObjectStruct ObjectUlong(ulong l)
        {
            var item = new DdwafObjectStruct();
            _objectUlongField(ref item, l);
            return item;
        }

        internal DdwafObjectStruct ObjectDouble(double b)
        {
            var item = new DdwafObjectStruct();
            _objectDoubleField(ref item, b);
            return item;
        }

        internal DdwafObjectStruct ObjectNull()
        {
            var item = new DdwafObjectStruct();
            _objectNullField(ref item);
            return item;
        }

        internal DdwafObjectStruct ObjectArray()
        {
            var item = new DdwafObjectStruct();
            _objectArrayField(ref item);
            return item;
        }

        internal DdwafObjectStruct ObjectMap()
        {
            var item = new DdwafObjectStruct();
            _objectMapField(ref item);
            return item;
        }

        internal bool ObjectArrayAdd(ref DdwafObjectStruct array, ref DdwafObjectStruct entry) => _objectArrayAddField(ref array, ref entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal bool ObjectMapAdd(ref DdwafObjectStruct map, string entryName, ulong entryNameLength, ref DdwafObjectStruct entry) => Environment.Is64BitProcess ? _objectMapAddFieldX64!(ref map, entryName, entryNameLength, ref entry) : _objectMapAddFieldX86!(ref map, entryName, (uint)entryNameLength, ref entry);

        private void LoggingCallback(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            uint line,
            string message,
            ulong message_len)
        {
            var location = $"[{function}]{file}({line})";
            switch (level)
            {
                case DDWAF_LOG_LEVEL.DDWAF_TRACE:
                case DDWAF_LOG_LEVEL.DDWAF_DEBUG:
                    Log.Debug("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_INFO:
                    Log.Information("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_WARN:
                    Log.Warning("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_ERROR:
                case DDWAF_LOG_LEVEL.DDWAF_AFTER_LAST:
                    Log.Error("{Level}: {Location}: {Message}", level, location, message);
                    break;
                default:
                    Log.Error("[Unknown level] {Level}: {Location}: {Message}", level, location, message);
                    break;
            }
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
            where T : Delegate
        {
            funcPtr = NativeLibrary.GetExport(handle, functionName);
            if (funcPtr == IntPtr.Zero)
            {
                Log.Error("No function of name {FunctionName} exists on waf object", functionName);
                ExportErrorHappened = true;
                return null;
            }

            Log.Debug("GetDelegateForNativeFunction {FunctionName} -  {FuncPtr}: ", functionName, funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate => GetDelegateForNativeFunction<T>(handle, functionName, out _);
    }
}
