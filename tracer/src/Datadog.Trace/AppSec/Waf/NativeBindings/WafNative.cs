// <copyright file="WafNative.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        private readonly ResultFreeDelegate _resultFreeField;
        private readonly InitContextDelegate _initContextField;
        private readonly RunDelegate _runField;
        private readonly DestroyDelegate _destroyField;
        private readonly ContextDestroyDelegate _contextDestroyField;
        private readonly ObjectInvalidDelegate _objectInvalidField;
        private readonly ObjectStringLengthDelegateX64 _objectStringLengthFieldX64;
        private readonly ObjectStringLengthDelegateX86 _objectStringLengthFieldX86;
        private readonly ObjectSignedDelegate _objectSignedField;
        private readonly ObjectUnsignedDelegate _objectUnsignField;
        private readonly ObjectArrayDelegate _objectArrayField;
        private readonly ObjectMapDelegate _objectMapField;
        private readonly ObjectArrayAddDelegate _objectArrayAddField;
        private readonly ObjectMapAddDelegateX64 _objectMapAddFieldX64;
        private readonly ObjectMapAddDelegateX86 _objectMapAddFieldX86;
        private readonly ObjectFreeDelegate _objectFreeField;
        private readonly IntPtr _objectFreeFuncPtrField;
        private readonly SetupLogCallbackDelegate setupLogCallbackField;

        /// <summary>
        /// Initializes a new instance of the <see cref="WafNative"/> class.
        /// </summary>
        /// <param name="handle">Can't be a null pointer. Waf library must be loaded by now</param>
        internal WafNative(IntPtr handle)
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
            // convert to a delegate and attempt to pin it by assigning it to  field
            setupLogCallbackField = LoggingCallback;
            // set the log level and setup the logger
            var level = GlobalSettings.Source.DebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_DEBUG : DDWAF_LOG_LEVEL.DDWAF_INFO;
            setupLogging(Marshal.GetFunctionPointerForDelegate(setupLogCallbackField), level);
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

        private enum DDWAF_LOG_LEVEL
        {
            DDWAF_TRACE,
            DDWAF_DEBUG,
            DDWAF_INFO,
            DDWAF_WARN,
            DDWAF_ERROR,
            DDWAF_AFTER_LAST,
        }

        internal IntPtr ObjectFreeFuncPtr
        {
            get
            {
                return _objectFreeFuncPtrField;
            }
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

        private void LoggingCallback(
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
            _log.Debug("GetDelegateForNativeFunction -  funcPtr: " + funcPtr);
            return (T)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(T));
        }

        private T GetDelegateForNativeFunction<T>(IntPtr handle, string functionName)
            where T : Delegate
        {
            return GetDelegateForNativeFunction<T>(handle, functionName, out var _);
        }
    }
}
