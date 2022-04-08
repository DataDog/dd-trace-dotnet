// <copyright file="WafNative.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor(typeof(WafNative));

        private readonly GetVersionDelegate _getVersionField;
        private readonly InitDelegate _initField;
        private readonly InitContextDelegate _initContextField;
        private readonly RunDelegate _runField;
        private readonly DestroyDelegate _destroyField;
        private readonly ContextDestroyDelegate _contextDestroyField;
        private readonly ObjectInvalidDelegate _objectInvalidField;
        private readonly ObjectStringLengthDelegate _objectStringLengthField;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="WafNative"/> class.
        /// </summary>
        /// <param name="handle">Can't be a null pointer. Waf library must be loaded by now</param>
        internal WafNative(IntPtr handle)
        {
            _initField = GetDelegateForNativeFunction<InitDelegate>(handle, "ddwaf_init");
            _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(handle, "ddwaf_context_init");
            _runField = GetDelegateForNativeFunction<RunDelegate>(handle, "ddwaf_run");
            _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(handle, "ddwaf_destroy");
            _contextDestroyField = GetDelegateForNativeFunction<ContextDestroyDelegate>(handle, "ddwaf_context_destroy");
            _objectInvalidField = GetDelegateForNativeFunction<ObjectInvalidDelegate>(handle, "ddwaf_object_invalid");
            _objectStringLengthField = GetDelegateForNativeFunction<ObjectStringLengthDelegate>(handle, "ddwaf_object_stringl");
            _objectArrayField = GetDelegateForNativeFunction<ObjectArrayDelegate>(handle, "ddwaf_object_array");
            _objectMapField = GetDelegateForNativeFunction<ObjectMapDelegate>(handle, "ddwaf_object_map");
            _objectArrayAddField = GetDelegateForNativeFunction<ObjectArrayAddDelegate>(handle, "ddwaf_object_array_add");
            _objectArrayGetIndex = GetDelegateForNativeFunction<ObjectArrayGetAtIndexDelegate>(handle, "ddwaf_object_get_index");
            _objectMapAddFieldX64 =
                Environment.Is64BitProcess ?
                    GetDelegateForNativeFunction<ObjectMapAddDelegateX64>(handle, "ddwaf_object_map_addl") :
                    null;
            _objectMapAddFieldX86 =
                Environment.Is64BitProcess ?
                    null :
                    GetDelegateForNativeFunction<ObjectMapAddDelegateX86>(handle, "ddwaf_object_map_addl");
            _freeObjectield = GetDelegateForNativeFunction<FreeObjectDelegate>(handle, "ddwaf_object_free", out _freeObjectFuncField);
            _freeResultField = GetDelegateForNativeFunction<FreeResultDelegate>(handle, "ddwaf_result_free");
            _rulesetInfoFreeField = GetDelegateForNativeFunction<FreeRulesetInfoDelegate>(handle, "ddwaf_ruleset_info_free");
            _getVersionField = GetDelegateForNativeFunction<GetVersionDelegate>(handle, "ddwaf_get_version");
            // setup logging
            var setupLogging = GetDelegateForNativeFunction<SetupLoggingDelegate>(handle, "ddwaf_set_log_cb");
            // convert to a delegate and attempt to pin it by assigning it to  field
            _setupLogCallbackField = new SetupLogCallbackDelegate(LoggingCallback);
            // set the log level and setup the logger
            var level = GlobalSettings.Source.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            setupLogging(_setupLogCallbackField, level);
        }

        private delegate void GetVersionDelegate(ref DdwafVersionStruct version);

        private delegate void FreeResultDelegate(ref DdwafResultStruct output);

        private delegate void FreeRulesetInfoDelegate(ref DdwafRuleSetInfoStruct output);

        private delegate IntPtr InitDelegate(IntPtr wafRule, ref DdwafConfigStruct config, ref DdwafRuleSetInfoStruct ruleSetInfo);

        private delegate IntPtr InitContextDelegate(IntPtr powerwafHandle, IntPtr objFree);

        private delegate IntPtr InitMetricsCollectorDelegate(IntPtr powerwafHandle);

        private delegate DDWAF_RET_CODE RunDelegate(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs);

        private delegate void DestroyDelegate(IntPtr handle);

        private delegate void ContextDestroyDelegate(IntPtr context);

        private delegate IntPtr ObjectInvalidDelegate(IntPtr emptyObjPtr);

        private delegate IntPtr ObjectStringLengthDelegate(IntPtr emptyObjPtr, string s, ulong length);

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

        internal IntPtr ObjectFreeFuncPtr => _freeObjectFuncField;

        internal DdwafVersionStruct GetVersion()
        {
            DdwafVersionStruct version = default;
            _getVersionField(ref version);
            return version;
        }

        internal IntPtr Init(IntPtr wafRule, ref DdwafConfigStruct config, ref DdwafRuleSetInfoStruct ruleSetInfo) => _initField(wafRule, ref config, ref ruleSetInfo);

        internal IntPtr InitContext(IntPtr powerwafHandle, IntPtr objFree) => _initContextField(powerwafHandle, objFree);

        internal DDWAF_RET_CODE Run(IntPtr context, IntPtr newArgs, ref DdwafResultStruct result, ulong timeLeftInUs) => _runField(context, newArgs, ref result, timeLeftInUs);

        internal void Destroy(IntPtr handle) => _destroyField(handle);

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
        internal bool ObjectMapAdd(IntPtr map, string entryName, ulong entryNameLength, IntPtr entry) => Environment.Is64BitProcess ?
                    _objectMapAddFieldX64(map, entryName, entryNameLength, entry) :
                    _objectMapAddFieldX86(map, entryName, (uint)entryNameLength, entry);

        internal void ObjectFreePtr(IntPtr input) => _freeObjectield(input);

        internal void ResultFree(ref DdwafResultStruct output) => _freeResultField(ref output);

        /// <summary>
        /// Only this function needs to be called on DdwafRuleSetInfoStruct, no need to dispose the Errors object inside because waf takes care of it
        /// </summary>
        /// <param name="output">the rulsetinfo structure</param>
        internal void RuleSetInfoFree(ref DdwafRuleSetInfoStruct output) => _rulesetInfoFreeField(ref output);

        private void LoggingCallback(
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

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName, out IntPtr funcPtr)
            where T : Delegate
        {
            funcPtr = NativeLibrary.GetExport(handle, functionName);
            if (funcPtr == IntPtr.Zero)
            {
                _log.Error("No function of name {functionName} exists on waf object", functionName);
                return null;
            }

            _log.Debug("GetDelegateForNativeFunction {functionName} -  {funcPtr}: ", functionName, funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate => GetDelegateForNativeFunction<T>(handle, functionName, out var _);
    }
}
