// <copyright file="WafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal class WafLibraryInvoker
    {
#if NETFRAMEWORK
        private const string DllName = "ddwaf.dll";
#else
        private const string DllName = "ddwaf";
#endif

        private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(WafLibraryInvoker));
        private readonly GetVersionDelegate _getVersionField;
        private readonly InitDelegate _initField;
        private readonly InitContextDelegate _initContextField;
        private readonly RunDelegate _runField;
        private readonly DestroyDelegate _destroyField;
        private readonly ContextDestroyDelegate _contextDestroyField;
        private readonly ObjectInvalidDelegate _objectInvalidField;
        private readonly ObjectStringLengthDelegate _objectStringLengthField;
        private readonly ObjectBoolDelegate _objectBoolField;
        private readonly ObjectArrayDelegate _objectArrayField;
        private readonly ObjectMapDelegate _objectMapField;
        private readonly ObjectArrayAddDelegate _objectArrayAddField;
        private readonly ObjectArrayGetAtIndexDelegate _objectArrayGetIndex;
        private readonly ObjectMapAddDelegateX64 _objectMapAddFieldX64;
        private readonly ObjectMapAddDelegateX86 _objectMapAddFieldX86;
        private readonly FreeResultDelegate _freeResultField;
        private readonly FreeObjectDelegate _freeObjectield;
        private readonly IntPtr _freeObjectFuncField;
        private readonly FreeRulesetInfoDelegate _rulesetInfoFreeField;
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
            _rulesetInfoFreeField = GetDelegateForNativeFunction<FreeRulesetInfoDelegate>(libraryHandle, "ddwaf_ruleset_info_free");
            _getVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(libraryHandle, "ddwaf_get_version");
            // setup logging
            var setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(libraryHandle, "ddwaf_set_log_cb");
            // convert to a delegate and attempt to pin it by assigning it to  field
            _setupLogCallbackField = new SetupLogCallbackDelegate(LoggingCallback);
            // set the log level and setup the logger
            var level = GlobalSettings.Instance.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            setupLogging(_setupLogCallbackField, level);
        }

        private delegate IntPtr GetVersionDelegate();

        private delegate void FreeResultDelegate(ref DdwafResultStruct output);

        private delegate void FreeRulesetInfoDelegate(DdwafRuleSetInfo output);

        private delegate IntPtr InitDelegate(IntPtr wafRule, ref DdwafConfigStruct config, DdwafRuleSetInfo ruleSetInfo);

        private delegate IntPtr UpdateDelegate(IntPtr oldWafHandle, IntPtr wafRule, DdwafRuleSetInfo ruleSetInfo);

        private delegate IntPtr InitContextDelegate(IntPtr wafHandle);

        private delegate DDWAF_RET_CODE RunDelegate(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs);

        private delegate void DestroyDelegate(IntPtr handle);

        private delegate void ContextDestroyDelegate(IntPtr context);

        private delegate IntPtr ObjectInvalidDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectStringLengthDelegate(IntPtr emptyObjPtr, string s, ulong length);

        private delegate IntPtr ObjectBoolDelegate(IntPtr emptyObjPtr, bool b);

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
                Log.Error("Lib name or runtime ids is null, current platform {fd} is likely not supported", fd.ToString());
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

        internal string GetVersion()
        {
            if (_version == null)
            {
                var ptr = _getVersionField();
                _version = Marshal.PtrToStringAnsi(ptr);
            }

            return _version;
        }

        internal IntPtr Init(IntPtr wafRule, ref DdwafConfigStruct config, DdwafRuleSetInfo ruleSetInfo) => _initField(wafRule, ref config, ruleSetInfo);

        /// <summary>
        /// Only give a non null ruleSetInfo when updating rules. When updating rules overrides, rules datas, the ruleSetInfo will return no error and no diagnostics, even if there are, it's misleading, so give null in this case.
        /// </summary>
        /// <param name="oldWafHandle">current waf handle</param>
        /// <param name="wafData">a pointer to the new waf data (rules or overrides or other)</param>
        /// <param name="ruleSetInfo">errors and diagnostics of the update, only for valid for new rules</param>
        /// <returns>the new waf handle, if error, will be a nullptr</returns>
        internal IntPtr Update(IntPtr oldWafHandle, IntPtr wafData, DdwafRuleSetInfo ruleSetInfo) => _updateField(oldWafHandle, wafData, ruleSetInfo);

        internal IntPtr InitContext(IntPtr powerwafHandle) => _initContextField(powerwafHandle);

        internal DDWAF_RET_CODE Run(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs) => _runField(context, newArgs, ref result, timeLeftInUs);

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

        internal void ObjectFreePtr(IntPtr input) => _freeObjectield(input);

        internal void ResultFree(ref DdwafResultStruct output) => _freeResultField(ref output);

        /// <summary>
        /// Only this function needs to be called on DdwafRuleSetInfoStruct, no need to dispose the Errors object inside because waf takes care of it
        /// </summary>
        /// <param name="output">the ruleset info structure</param>
        internal void RuleSetInfoFree(DdwafRuleSetInfo output) => _rulesetInfoFreeField(output);

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
                    _log.Debug("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_INFO:
                    _log.Information("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_WARN:
                    _log.Warning("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_ERROR:
                case DDWAF_LOG_LEVEL.DDWAF_AFTER_LAST:
                    _log.Error("{Level}: {Location}: {Message}", level, location, message);
                    break;
                default:
                    _log.Error("[Unknown level] {Level}: {Location}: {Message}", level, location, message);
                    break;
            }
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
            where T : Delegate
        {
            funcPtr = NativeLibrary.GetExport(handle, functionName);
            if (funcPtr == IntPtr.Zero)
            {
                _log.Error("No function of name {FunctionName} exists on waf object", functionName);
                ExportErrorHappened = true;
                return null;
            }

            _log.Debug("GetDelegateForNativeFunction {FunctionName} -  {FuncPtr}: ", functionName, funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate => GetDelegateForNativeFunction<T>(handle, functionName, out _);
    }
}
