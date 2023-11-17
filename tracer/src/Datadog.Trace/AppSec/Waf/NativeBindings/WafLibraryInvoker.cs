// <copyright file="WafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class WafLibraryInvoker
    {
#if NETFRAMEWORK
        private const string DllName = "ddwaf.dll";
#else
        private const string DllName = "ddwaf";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafLibraryInvoker));
        private readonly GetVersionDelegate _getVersionField;
        private readonly InitDelegate _initField;
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
        private readonly FreeResultDelegate _freeResultField;
        private readonly FreeObjectDelegate _freeObjectield;
        private readonly IntPtr _freeObjectFuncField;
        private readonly SetupLoggingDelegate _setupLogging;
        private readonly SetupLogCallbackDelegate _setupLogCallbackField;
        private readonly UpdateDelegate _updateField;
        private string _version = null;

        private WafLibraryInvoker(IntPtr libraryHandle)
        {
            ExportErrorHappened = false;
            _initField = GetDelegateForNativeFunction<InitDelegate>(libraryHandle, "ddwaf_init");
            _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(libraryHandle, "ddwaf_context_init");
            _runField = GetDelegateForNativeFunction<RunDelegate>(libraryHandle, "ddwaf_run");
            _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(libraryHandle, "ddwaf_destroy");
            _updateField = GetDelegateForNativeFunction<UpdateDelegate>(libraryHandle, "ddwaf_update");
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
            _freeObjectield = GetDelegateForNativeFunction<FreeObjectDelegate>(libraryHandle, "ddwaf_object_free", out _freeObjectFuncField);
            _freeResultField = GetDelegateForNativeFunction<FreeResultDelegate>(libraryHandle, "ddwaf_result_free");
            _getVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(libraryHandle, "ddwaf_get_version");
            // setup logging
            _setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(libraryHandle, "ddwaf_set_log_cb");
            // convert to a delegate and attempt to pin it by assigning it to  field
            _setupLogCallbackField = new SetupLogCallbackDelegate(LoggingCallback);
        }

        private delegate IntPtr GetVersionDelegate();

        private delegate void FreeResultDelegate(ref DdwafResultStruct output);

        private delegate IntPtr InitDelegate(ref DdwafObjectStruct wafRule, ref DdwafConfigStruct config, ref DdwafObjectStruct diagnostics);

        private delegate IntPtr UpdateDelegate(IntPtr oldWafHandle, ref DdwafObjectStruct wafRule, ref DdwafObjectStruct diagnostics);

        private delegate IntPtr InitContextDelegate(IntPtr wafHandle);

        private delegate WafReturnCode RunDelegate(IntPtr context, ref DdwafObjectStruct persistentData, ref DdwafObjectStruct ephemeralData, ref DdwafResultStruct result, ulong timeLeftInUs);

        private delegate void DestroyDelegate(IntPtr handle);

        private delegate void ContextDestroyDelegate(IntPtr context);

        private delegate IntPtr ObjectInvalidDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectStringLengthDelegate(IntPtr emptyObjPtr, string s, ulong length);

        private delegate IntPtr ObjectBoolDelegate(IntPtr emptyObjPtr, bool b);

        private delegate IntPtr ObjectDoubleDelegate(IntPtr emptyObjPtr, double value);

        private delegate IntPtr ObjectNullDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectUlongDelegate(IntPtr emptyObjPtr, ulong value);

        private delegate IntPtr ObjectLongDelegate(IntPtr emptyObjPtr, long value);

        private delegate IntPtr ObjectArrayDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectMapDelegate(IntPtr emptyObjPtr);

        private delegate bool ObjectArrayAddDelegate(IntPtr array, IntPtr entry);

        private delegate IntPtr ObjectArrayGetAtIndexDelegate(IntPtr array, long index);

        private delegate bool ObjectMapAddDelegateX64(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry);

        private delegate bool ObjectMapAddDelegateX86(IntPtr map, string entryName, uint entryNameLength, IntPtr entry);

        private delegate void FreeObjectDelegate(IntPtr input);

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

        internal IntPtr ObjectFreeFuncPtr => _freeObjectFuncField;

        /// <summary>
        /// Initializes static members of the <see cref="WafLibraryInvoker"/> class.
        /// </summary>
        /// <param name="libVersion">can be null, means use a specific version in the name of the loaded file </param>
        internal static LibraryInitializationResult Initialize(string libVersion = null)
        {
            var fd = FrameworkDescription.Instance;

            var libName = LibraryLocationHelper.GetLibName(fd, libVersion);
            var runtimeIds = LibraryLocationHelper.GetRuntimeIds(fd);

            // libName or runtimeIds being null means platform is not supported
            // no point attempting to load the library
            IntPtr libraryHandle;
            if (libName != null && runtimeIds != null)
            {
                var paths = LibraryLocationHelper.GetDatadogNativeFolders(fd, runtimeIds);
                if (!LibraryLocationHelper.TryLoadLibraryFromPaths(libName, paths, out libraryHandle))
                {
                    return LibraryInitializationResult.FromLibraryLoadError();
                }
            }
            else
            {
                Log.Error("Lib name or runtime ids is null, current platform {Fd} is likely not supported", fd.ToString());
                return LibraryInitializationResult.FromPlatformNotSupported();
            }

            var wafLibraryInvoker = new WafLibraryInvoker(libraryHandle);
            if (wafLibraryInvoker.ExportErrorHappened)
            {
                Log.Error("Waf library couldn't initialize properly because of missing methods in native library, please make sure the tracer has been correctly installed and that previous versions are correctly uninstalled.");
                return LibraryInitializationResult.FromExportErrorHappened();
            }

            return LibraryInitializationResult.FromSuccess(wafLibraryInvoker);
        }

        internal void SetupLogging(bool instanceDebugEnabled)
        {
            var level = instanceDebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            _setupLogging(_setupLogCallbackField, level);
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

        internal IntPtr Init(ref DdwafObjectStruct wafRule, ref DdwafConfigStruct config, ref DdwafObjectStruct diagnostics) => _initField(ref wafRule, ref config, ref diagnostics);

        /// <summary>
        /// Only give a non null ruleSetInfo when updating rules. When updating rules overrides, rules datas, the ruleSetInfo will return no error and no diagnostics, even if there are, it's misleading, so give null in this case.
        /// </summary>
        /// <param name="oldWafHandle">current waf handle</param>
        /// <param name="wafData">a pointer to the new waf data (rules or overrides or other)</param>
        /// <param name="diagnostics">errors and diagnostics of the update, only for valid for new rules</param>
        /// <returns>the new waf handle, if error, will be a nullptr</returns>
        internal IntPtr Update(IntPtr oldWafHandle, ref DdwafObjectStruct wafData, ref DdwafObjectStruct diagnostics) => _updateField(oldWafHandle, ref wafData, ref diagnostics);

        internal IntPtr InitContext(IntPtr powerwafHandle) => _initContextField(powerwafHandle);

        internal WafReturnCode Run(IntPtr context, ref DdwafObjectStruct persistentData, ref DdwafObjectStruct ephemeralData, ref DdwafResultStruct result, ulong timeLeftInUs) => _runField(context, ref persistentData, ref ephemeralData, ref result, timeLeftInUs);

        internal void Destroy(IntPtr wafHandle) => _destroyField(wafHandle);

        internal void ContextDestroy(IntPtr handle) => _contextDestroyField(handle);

        internal IntPtr ObjectArrayGetIndex(IntPtr array, long index) => _objectArrayGetIndex(array, index);

        internal IntPtr ObjectInvalid()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectInvalidField(ptr);
            return ptr;
        }

        internal IntPtr ObjectStringLength(string s, ulong length)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectStringLengthField(ptr, s, length);
            return ptr;
        }

        internal IntPtr ObjectBool(bool b)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectBoolField(ptr, b);
            return ptr;
        }

        internal IntPtr ObjectLong(long l)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectLongField(ptr, l);
            return ptr;
        }

        internal IntPtr ObjectUlong(ulong l)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectUlongField(ptr, l);
            return ptr;
        }

        internal IntPtr ObjectDouble(double b)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectDoubleField(ptr, b);
            return ptr;
        }

        internal IntPtr ObjectNull()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectNullField(ptr);
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

        internal bool ObjectArrayAdd(IntPtr array, IntPtr entry) => _objectArrayAddField(array, entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal bool ObjectMapAdd(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry) => Environment.Is64BitProcess ? _objectMapAddFieldX64!(map, entryName, entryNameLength, entry) : _objectMapAddFieldX86!(map, entryName, (uint)entryNameLength, entry);

        internal void ObjectFreePtr(ref IntPtr input)
        {
            _freeObjectield(input);
            input = IntPtr.Zero;
        }

        internal void ResultFree(ref DdwafResultStruct output) => _freeResultField(ref output);

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
