// <copyright file="WafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal static class WafLibraryInvoker
    {
#if NETFRAMEWORK
        private const string DllName = "ddwaf.dll";
#else
        private const string DllName = "ddwaf";
#endif

        private static IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(WafLibraryInvoker));
        private static GetVersionDelegate _getVersionField;
        private static InitDelegate _initField;
        private static UpdateRuleDelegate _updateRuleField;
        private static UpdateRuleDelegate _toggleRulesField;
        private static InitContextDelegate _initContextField;
        private static RunDelegate _runField;
        private static DestroyDelegate _destroyField;
        private static ContextDestroyDelegate _contextDestroyField;
        private static ObjectInvalidDelegate _objectInvalidField;
        private static ObjectStringLengthDelegate _objectStringLengthField;
        private static ObjectBoolDelegate _objectBoolField;
        private static ObjectArrayDelegate _objectArrayField;
        private static ObjectMapDelegate _objectMapField;
        private static ObjectArrayAddDelegate _objectArrayAddField;
        private static ObjectArrayGetAtIndexDelegate _objectArrayGetIndex;
        private static ObjectMapAddDelegateX64 _objectMapAddFieldX64;
        private static ObjectMapAddDelegateX86 _objectMapAddFieldX86;
        private static FreeResultDelegate _freeResultField;
        private static FreeObjectDelegate _freeObjectield;
        private static IntPtr _freeObjectFuncField;
        private static FreeRulesetInfoDelegate _rulesetInfoFreeField;
        private static SetupLogCallbackDelegate _setupLogCallbackField;
        private static string version = null;

        private delegate IntPtr GetVersionDelegate();

        private delegate void FreeResultDelegate(ref DdwafResultStruct output);

        private delegate void FreeRulesetInfoDelegate(ref DdwafRuleSetInfoStruct output);

        private delegate IntPtr InitDelegate(IntPtr wafRule, ref DdwafConfigStruct config, ref DdwafRuleSetInfoStruct ruleSetInfo);

        private delegate DDWAF_RET_CODE UpdateRuleDelegate(IntPtr powerwafHandle, IntPtr data);

        private delegate IntPtr InitContextDelegate(IntPtr powerwafHandle);

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

        internal static bool ExportErrorHappened { get; private set; }

        internal static IntPtr ObjectFreeFuncPtr => _freeObjectFuncField;

        /// <summary>
        /// Initializes static members of the <see cref="WafLibraryInvoker"/> class.
        /// </summary>
        /// <param name="libraryHandle">Can't be a null pointer. Waf library must be loaded by now</param>
        internal static void InitializeExports(IntPtr libraryHandle)
        {
            _initField = GetDelegateForNativeFunction<InitDelegate>(libraryHandle, "ddwaf_init");
            _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(libraryHandle, "ddwaf_context_init");
            _runField = GetDelegateForNativeFunction<RunDelegate>(libraryHandle, "ddwaf_run");
            _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(libraryHandle, "ddwaf_destroy");
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
            _updateRuleField = GetDelegateForNativeFunction<UpdateRuleDelegate>(libraryHandle, "ddwaf_update_rule_data");
            _toggleRulesField = GetDelegateForNativeFunction<UpdateRuleDelegate>(libraryHandle, "ddwaf_toggle_rules");
            // setup logging
            var setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(libraryHandle, "ddwaf_set_log_cb");
            // convert to a delegate and attempt to pin it by assigning it to  field
            _setupLogCallbackField = new SetupLogCallbackDelegate(LoggingCallback);
            // set the log level and setup the logger
            var level = GlobalSettings.Instance.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            setupLogging(_setupLogCallbackField, level);
        }

        internal static string GetVersion()
        {
            if (version == null)
            {
                var ptr = _getVersionField();
                version = Marshal.PtrToStringAnsi(ptr);
            }

            return version;
        }

        internal static IntPtr Init(IntPtr wafRule, ref DdwafConfigStruct config, ref DdwafRuleSetInfoStruct ruleSetInfo) => _initField(wafRule, ref config, ref ruleSetInfo);

        internal static DDWAF_RET_CODE UpdateRuleData(IntPtr powerwafHandle, IntPtr data) => _updateRuleField(powerwafHandle, data);

        internal static DDWAF_RET_CODE ToggleRules(IntPtr powerwafHandle, IntPtr data) => _toggleRulesField(powerwafHandle, data);

        internal static IntPtr InitContext(IntPtr powerwafHandle) => _initContextField(powerwafHandle);

        internal static DDWAF_RET_CODE Run(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs) => _runField(context, newArgs, ref result, timeLeftInUs);

        internal static void Destroy(IntPtr handle) => _destroyField(handle);

        internal static void ContextDestroy(IntPtr handle) => _contextDestroyField(handle);

        internal static IntPtr ObjectArrayGetIndex(IntPtr array, long index) => _objectArrayGetIndex(array, index);

        internal static IntPtr ObjectInvalid()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectInvalidField(ptr);
            return ptr;
        }

        internal static IntPtr ObjectStringLength(string s, ulong length)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectStringLengthField(ptr, s, length);
            return ptr;
        }

        internal static IntPtr ObjectBool(bool b)
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectBoolField(ptr, b);
            return ptr;
        }

        internal static IntPtr ObjectArray()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectArrayField(ptr);
            return ptr;
        }

        internal static IntPtr ObjectMap()
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(DdwafObjectStruct)));
            _objectMapField(ptr);
            return ptr;
        }

        internal static bool ObjectArrayAdd(IntPtr array, IntPtr entry) => _objectArrayAddField(array, entry);

        // Setting entryNameLength to 0 will result in the entryName length being re-computed with strlen
        internal static bool ObjectMapAdd(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry) => Environment.Is64BitProcess ? _objectMapAddFieldX64(map, entryName, entryNameLength, entry) : _objectMapAddFieldX86(map, entryName, (uint)entryNameLength, entry);

        internal static void ObjectFreePtr(IntPtr input) => _freeObjectield(input);

        internal static void ResultFree(ref DdwafResultStruct output) => _freeResultField(ref output);

        /// <summary>
        /// Only this function needs to be called on DdwafRuleSetInfoStruct, no need to dispose the Errors object inside because waf takes care of it
        /// </summary>
        /// <param name="output">the rulsetinfo structure</param>
        internal static void RuleSetInfoFree(ref DdwafRuleSetInfoStruct output) => _rulesetInfoFreeField(ref output);

        private static void LoggingCallback(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            uint line,
            string message,
            ulong message_len)
        {
            var formattedMessage = $"{level}: [{function}]{file}({line}): {message}";
            switch (level)
            {
                case DDWAF_LOG_LEVEL.DDWAF_TRACE:
                case DDWAF_LOG_LEVEL.DDWAF_DEBUG:
                    _log.Debug(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_INFO:
                    _log.Information(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_WARN:
                    _log.Warning(formattedMessage);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_ERROR:
                case DDWAF_LOG_LEVEL.DDWAF_AFTER_LAST:
                    _log.Error(formattedMessage);
                    break;
                default:
                    _log.Error("[Unknown level] " + formattedMessage);
                    break;
            }
        }

        private static T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
            where T : Delegate
        {
            funcPtr = NativeLibrary.GetExport(handle, functionName);
            if (funcPtr == IntPtr.Zero)
            {
                _log.Error("No function of name {functionName} exists on waf object", functionName);
                ExportErrorHappened = true;
                return null;
            }

            _log.Debug("GetDelegateForNativeFunction {functionName} -  {funcPtr}: ", functionName, funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private static T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate => GetDelegateForNativeFunction<T>(handle, functionName, out var _);
    }
}
