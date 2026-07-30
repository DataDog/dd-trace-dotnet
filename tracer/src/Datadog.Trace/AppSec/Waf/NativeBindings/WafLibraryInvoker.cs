// <copyright file="WafLibraryInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.Logging;

#pragma warning disable SA1401

namespace Datadog.Trace.AppSec.Waf.NativeBindings
{
    internal sealed class WafLibraryInvoker : IWafLibraryInvoker
    {
        /// <summary>
        /// Lowest libddwaf major version this binding can talk to. libddwaf 2.0 redesigned the C API
        /// (allocators, 16 byte objects, subcontexts), so 1.x is not merely deprecated, it is ABI
        /// incompatible and must be rejected rather than loaded.
        /// </summary>
        private const int MinimumWafMajorVersion = 2;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafLibraryInvoker));
        private readonly GetVersionDelegate _getVersionField;

        private readonly BuilderInitDelegate _builderInitField;
        private readonly BuilderAddOrUpdateConfigDelegate _builderAddOrUpdateConfigField;
        private readonly BuilderRemoveConfigDelegate _builderRemoveConfigDelegate;
        private readonly BuilderBuildInstanceDelegate _builderBuildInstanceDelegate;
        private readonly BuilderDestroyDelegate _builderDestroyDelegate;

        private readonly InitContextDelegate _initContextField;
        private readonly ContextEvalDelegate _contextEvalField;
        private readonly ContextDestroyDelegate _contextDestroyField;
        private readonly SubcontextInitDelegate _subcontextInitField;
        private readonly SubcontextEvalDelegate _subcontextEvalField;
        private readonly SubcontextDestroyDelegate _subcontextDestroyField;
        private readonly DestroyDelegate _destroyField;

        private readonly GetDefaultAllocatorDelegate _getDefaultAllocatorField;
        private readonly ObjectDestroyDelegate _objectDestroyField;

        private readonly ObjectSetInvalidDelegate _objectSetInvalidField;
        private readonly ObjectSetNullDelegate _objectSetNullField;
        private readonly ObjectSetStringDelegate _objectSetStringField;
        private readonly ObjectSetBoolDelegate _objectSetBoolField;
        private readonly ObjectSetSignedDelegate _objectSetSignedField;
        private readonly ObjectSetUnsignedDelegate _objectSetUnsignedField;
        private readonly ObjectSetFloatDelegate _objectSetFloatField;
        private readonly ObjectSetArrayDelegate _objectSetArrayField;
        private readonly ObjectSetMapDelegate _objectSetMapField;
        private readonly ObjectInsertDelegate _objectInsertField;
        private readonly ObjectInsertKeyDelegate _objectInsertKeyField;

        private readonly SetupLoggingDelegate _setupLogging;
        private readonly SetupLogCallbackDelegate _setupLogCallbackField;
        private readonly GetKnownAddressesDelegate _getKnownAddresses;
        private string _version;
        private bool _isKnownAddressesSuported;
        private IntPtr _defaultAllocator;

        private WafLibraryInvoker(IntPtr libraryHandle, string libVersion = null)
        {
            ExportErrorHappened = false;

            _builderInitField = GetDelegateForNativeFunction<BuilderInitDelegate>(libraryHandle, "ddwaf_builder_init");
            _builderAddOrUpdateConfigField = GetDelegateForNativeFunction<BuilderAddOrUpdateConfigDelegate>(libraryHandle, "ddwaf_builder_add_or_update_config");
            _builderRemoveConfigDelegate = GetDelegateForNativeFunction<BuilderRemoveConfigDelegate>(libraryHandle, "ddwaf_builder_remove_config");
            _builderBuildInstanceDelegate = GetDelegateForNativeFunction<BuilderBuildInstanceDelegate>(libraryHandle, "ddwaf_builder_build_instance");
            _builderDestroyDelegate = GetDelegateForNativeFunction<BuilderDestroyDelegate>(libraryHandle, "ddwaf_builder_destroy");

            _initContextField = GetDelegateForNativeFunction<InitContextDelegate>(libraryHandle, "ddwaf_context_init");
            _contextEvalField = GetDelegateForNativeFunction<ContextEvalDelegate>(libraryHandle, "ddwaf_context_eval");
            _contextDestroyField = GetDelegateForNativeFunction<ContextDestroyDelegate>(libraryHandle, "ddwaf_context_destroy");
            _subcontextInitField = GetDelegateForNativeFunction<SubcontextInitDelegate>(libraryHandle, "ddwaf_subcontext_init");
            _subcontextEvalField = GetDelegateForNativeFunction<SubcontextEvalDelegate>(libraryHandle, "ddwaf_subcontext_eval");
            _subcontextDestroyField = GetDelegateForNativeFunction<SubcontextDestroyDelegate>(libraryHandle, "ddwaf_subcontext_destroy");
            _destroyField = GetDelegateForNativeFunction<DestroyDelegate>(libraryHandle, "ddwaf_destroy");

            _getDefaultAllocatorField = GetDelegateForNativeFunction<GetDefaultAllocatorDelegate>(libraryHandle, "ddwaf_get_default_allocator");
            _objectDestroyField = GetDelegateForNativeFunction<ObjectDestroyDelegate>(libraryHandle, "ddwaf_object_destroy");

            _objectSetInvalidField = GetDelegateForNativeFunction<ObjectSetInvalidDelegate>(libraryHandle, "ddwaf_object_set_invalid");
            _objectSetNullField = GetDelegateForNativeFunction<ObjectSetNullDelegate>(libraryHandle, "ddwaf_object_set_null");
            _objectSetStringField = GetDelegateForNativeFunction<ObjectSetStringDelegate>(libraryHandle, "ddwaf_object_set_string");
            _objectSetBoolField = GetDelegateForNativeFunction<ObjectSetBoolDelegate>(libraryHandle, "ddwaf_object_set_bool");
            _objectSetSignedField = GetDelegateForNativeFunction<ObjectSetSignedDelegate>(libraryHandle, "ddwaf_object_set_signed");
            _objectSetUnsignedField = GetDelegateForNativeFunction<ObjectSetUnsignedDelegate>(libraryHandle, "ddwaf_object_set_unsigned");
            _objectSetFloatField = GetDelegateForNativeFunction<ObjectSetFloatDelegate>(libraryHandle, "ddwaf_object_set_float");
            _objectSetArrayField = GetDelegateForNativeFunction<ObjectSetArrayDelegate>(libraryHandle, "ddwaf_object_set_array");
            _objectSetMapField = GetDelegateForNativeFunction<ObjectSetMapDelegate>(libraryHandle, "ddwaf_object_set_map");
            _objectInsertField = GetDelegateForNativeFunction<ObjectInsertDelegate>(libraryHandle, "ddwaf_object_insert");
            _objectInsertKeyField = GetDelegateForNativeFunction<ObjectInsertKeyDelegate>(libraryHandle, "ddwaf_object_insert_key");

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

        // Every delegate below binds a C export of libddwaf, which is cdecl. The default convention
        // is Winapi, i.e. stdcall on Windows, so on win-x86 the callee wouldn't clean up the stack the
        // way the caller expects. It makes no difference on x64/arm64, where there is a single
        // convention, but it has to be spelled out for the 32-bit targets we still ship.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr BuilderInitDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool BuilderAddOrUpdateConfigDelegate(IntPtr builder, byte[] path, uint pathLen, ref DdwafObjectStruct config, ref DdwafObjectStruct diagnostics);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool BuilderRemoveConfigDelegate(IntPtr builder, byte[] path, uint pathLen);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr BuilderBuildInstanceDelegate(IntPtr builder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BuilderDestroyDelegate(IntPtr builder);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr InitContextDelegate(IntPtr wafHandle, IntPtr outputAlloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate WafReturnCode ContextEvalDelegate(IntPtr context, DdwafObjectStruct* data, IntPtr alloc, ref DdwafObjectStruct result, ulong timeLeftInUs);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ContextDestroyDelegate(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr SubcontextInitDelegate(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate WafReturnCode SubcontextEvalDelegate(IntPtr subcontext, DdwafObjectStruct* data, IntPtr alloc, ref DdwafObjectStruct result, ulong timeLeftInUs);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SubcontextDestroyDelegate(IntPtr subcontext);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DestroyDelegate(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetDefaultAllocatorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ObjectDestroyDelegate(ref DdwafObjectStruct obj, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetInvalidDelegate(DdwafObjectStruct* obj);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetNullDelegate(DdwafObjectStruct* obj);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetStringDelegate(DdwafObjectStruct* obj, byte[] str, uint length, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetBoolDelegate(DdwafObjectStruct* obj, bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetSignedDelegate(DdwafObjectStruct* obj, long value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetUnsignedDelegate(DdwafObjectStruct* obj, ulong value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetFloatDelegate(DdwafObjectStruct* obj, double value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetArrayDelegate(DdwafObjectStruct* obj, ushort capacity, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectSetMapDelegate(DdwafObjectStruct* obj, ushort capacity, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectInsertDelegate(DdwafObjectStruct* array, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private unsafe delegate DdwafObjectStruct* ObjectInsertKeyDelegate(DdwafObjectStruct* map, byte[] key, uint length, IntPtr alloc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetKnownAddressesDelegate(IntPtr wafHandle, ref uint size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetupLogCallbackDelegate(
            DDWAF_LOG_LEVEL level,
            string function,
            string file,
            uint line,
            string message,
            ulong message_len);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool SetupLoggingDelegate(SetupLogCallbackDelegate cb, DDWAF_LOG_LEVEL min_level);

        private enum DDWAF_LOG_LEVEL
        {
            DDWAF_LOG_TRACE = 0,
            DDWAF_LOG_DEBUG = 1,
            DDWAF_LOG_INFO = 2,
            DDWAF_LOG_WARN = 3,
            DDWAF_LOG_ERROR = 4,
            DDWAF_LOG_OFF = 5,
        }

        internal bool ExportErrorHappened { get; private set; }

        /// <summary>
        /// Gets the default allocator, used for every object libddwaf allocates on our behalf
        /// (eval results, diagnostics) and for the objects the legacy encoder asks it to build.
        /// Destroying it is a no-op, so it never needs to be released.
        /// </summary>
        internal IntPtr DefaultAllocator
        {
            get
            {
                if (_defaultAllocator == IntPtr.Zero)
                {
                    _defaultAllocator = _getDefaultAllocatorField();
                }

                return _defaultAllocator;
            }
        }

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

            var canParse = int.TryParse(versionWafSplit[1], out _);
            canParse &= int.TryParse(versionWafSplit[0], out var wafMajor);
            var tracerVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (tracerVersion is null || !canParse)
            {
                Log.Warning("Waf version {WafVersion} or tracer version {TracerVersion} have a non expected format", versionWaf, tracerVersion);
                return false;
            }

            // Waf version 2.0 or higher needed: 1.x has an incompatible ABI
            if (wafMajor < MinimumWafMajorVersion)
            {
                Log.Warning("Waf version {WafVersion} is not compatible with tracer version {TracerVersion}", versionWaf, tracerVersion);
                return false;
            }

            return true;
        }

        internal void SetupLogging(bool wafDebugEnabled)
        {
            var logLevel = wafDebugEnabled ? DDWAF_LOG_LEVEL.DDWAF_LOG_DEBUG : DDWAF_LOG_LEVEL.DDWAF_LOG_INFO;
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
                }

                _isKnownAddressesSuported = !string.IsNullOrEmpty(_version) && new Version(_version).Major >= MinimumWafMajorVersion;
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

        internal IntPtr InitBuilder() => _builderInitField();

        internal bool BuilderAddOrUpdateConfig(IntPtr builder, string path, ref DdwafObjectStruct config, ref DdwafObjectStruct diagnostics)
        {
            var pathBytes = GetConfigPathBytes(path, out var pathLen);
            return _builderAddOrUpdateConfigField(builder, pathBytes, pathLen, ref config, ref diagnostics);
        }

        internal bool BuilderRemoveConfig(IntPtr builder, string path)
        {
            var pathBytes = GetConfigPathBytes(path, out var pathLen);
            return _builderRemoveConfigDelegate(builder, pathBytes, pathLen);
        }

        internal IntPtr BuilderBuildInstance(IntPtr builder) => _builderBuildInstanceDelegate(builder);

        /// <summary>
        /// Releases a builder and every configuration it holds. Any WAF instance already built from it
        /// stays valid, as instances own their own copy of the ruleset.
        /// </summary>
        internal void DestroyBuilder(IntPtr builder) => _builderDestroyDelegate(builder);

        /// <summary>
        /// Converts a builder configuration path to the bytes libddwaf expects. The native side takes
        /// the path as a length delimited byte string (<c>std::string_view{path, path_len}</c>), so the
        /// length has to be a byte count: a character count would truncate, or overrun, any path that
        /// isn't pure ASCII. Encoding it ourselves also keeps the bytes identical on every platform,
        /// which matters because paths are compared byte for byte when a configuration is removed.
        /// </summary>
        private static byte[] GetConfigPathBytes(string path, out uint length)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            length = (uint)bytes.Length;
            return bytes;
        }

        /// <summary>
        /// Creates an evaluation context. The output allocator given here is the one libddwaf uses to
        /// allocate every eval result, so results must be released with the same allocator.
        /// </summary>
        /// <param name="powerwafHandle">the waf handle</param>
        /// <returns>the context handle, or <see cref="IntPtr.Zero"/> on failure</returns>
        internal IntPtr InitContext(IntPtr powerwafHandle) => _initContextField(powerwafHandle, DefaultAllocator);

        /// <summary>
        /// Evaluates persistent data on the context.
        /// WARNING: the data is retained by the context, so it must stay alive until the context is
        /// destroyed. We deliberately pass a null allocator so that libddwaf never frees it: ownership
        /// of the input buffers stays with the encoder, which releases them through IEncodeResult.
        /// </summary>
        /// <param name="context">waf context, can sustain multiple runs, args are cached</param>
        /// <param name="data">these pointers SHOULD remain alive until the context is disposed</param>
        /// <param name="result">Result, allocated with the context's output allocator</param>
        /// <param name="timeLeftInUs">timeout</param>
        /// <returns>Return waf code</returns>
        internal unsafe WafReturnCode ContextEval(IntPtr context, DdwafObjectStruct* data, ref DdwafObjectStruct result, ulong timeLeftInUs)
            => _contextEvalField(context, data, IntPtr.Zero, ref result, timeLeftInUs);

        internal IntPtr SubcontextInit(IntPtr context) => _subcontextInitField(context);

        /// <summary>
        /// Evaluates data on a subcontext, which is how ephemeral data is modelled since libddwaf 2.0.
        /// The same null allocator rule as <see cref="ContextEval"/> applies: the data must outlive the
        /// subcontext and is freed by us, not by libddwaf.
        /// </summary>
        /// <param name="subcontext">waf subcontext</param>
        /// <param name="data">these pointers SHOULD remain alive until the subcontext is destroyed</param>
        /// <param name="result">Result, allocated with the parent context's output allocator</param>
        /// <param name="timeLeftInUs">timeout</param>
        /// <returns>Return waf code</returns>
        internal unsafe WafReturnCode SubcontextEval(IntPtr subcontext, DdwafObjectStruct* data, ref DdwafObjectStruct result, ulong timeLeftInUs)
            => _subcontextEvalField(subcontext, data, IntPtr.Zero, ref result, timeLeftInUs);

        internal void Destroy(IntPtr wafHandle) => _destroyField(wafHandle);

        public void ContextDestroy(IntPtr handle) => _contextDestroyField(handle);

        public void SubcontextDestroy(IntPtr handle) => _subcontextDestroyField(handle);

        /// <summary>
        /// Releases an object and everything it owns. The allocator must be the one the object was
        /// built with, otherwise the heap is corrupted.
        /// </summary>
        public void ObjectDestroy(ref DdwafObjectStruct input, IntPtr alloc) => _objectDestroyField(ref input, alloc);

        /// <summary>
        /// Releases an object allocated by libddwaf on our behalf (an eval result or diagnostics),
        /// all of which use the default allocator.
        /// </summary>
        public void ObjectDestroy(ref DdwafObjectStruct input) => _objectDestroyField(ref input, DefaultAllocator);

        internal unsafe void ObjectSetInvalid(DdwafObjectStruct* obj) => _objectSetInvalidField(obj);

        internal unsafe void ObjectSetNull(DdwafObjectStruct* obj) => _objectSetNullField(obj);

        /// <summary>
        /// Sets a string value. libddwaf copies the bytes, inlining them in the object itself when
        /// there are 14 or fewer (a small string, which needs no allocation).
        /// </summary>
        /// <param name="obj">the object to write into</param>
        /// <param name="utf8Bytes">the UTF-8 bytes of the string</param>
        /// <param name="length">the number of bytes to copy, which must not exceed <paramref name="utf8Bytes"/></param>
        internal unsafe void ObjectSetString(DdwafObjectStruct* obj, byte[] utf8Bytes, uint length) => _objectSetStringField(obj, utf8Bytes, length, DefaultAllocator);

        internal unsafe void ObjectSetBool(DdwafObjectStruct* obj, bool value) => _objectSetBoolField(obj, value);

        internal unsafe void ObjectSetSigned(DdwafObjectStruct* obj, long value) => _objectSetSignedField(obj, value);

        internal unsafe void ObjectSetUnsigned(DdwafObjectStruct* obj, ulong value) => _objectSetUnsignedField(obj, value);

        internal unsafe void ObjectSetFloat(DdwafObjectStruct* obj, double value) => _objectSetFloatField(obj, value);

        internal unsafe void ObjectSetArray(DdwafObjectStruct* obj, ushort capacity) => _objectSetArrayField(obj, capacity, DefaultAllocator);

        internal unsafe void ObjectSetMap(DdwafObjectStruct* obj, ushort capacity) => _objectSetMapField(obj, capacity, DefaultAllocator);

        /// <summary>
        /// Appends an element to an array and returns a pointer to the (uninitialised) slot to fill in.
        /// </summary>
        /// <returns>the slot to write the value into, or null if the array is full</returns>
        internal unsafe DdwafObjectStruct* ObjectInsert(DdwafObjectStruct* array) => _objectInsertField(array, DefaultAllocator);

        /// <summary>
        /// Adds a key to a map and returns a pointer to the (uninitialised) value slot to fill in.
        /// The key is copied by libddwaf.
        /// </summary>
        /// <returns>the value slot to write into, or null if the map is full</returns>
        internal unsafe DdwafObjectStruct* ObjectInsertKey(DdwafObjectStruct* map, byte[] utf8Key, uint length) => _objectInsertKeyField(map, utf8Key, length, DefaultAllocator);

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
                case DDWAF_LOG_LEVEL.DDWAF_LOG_TRACE:
                case DDWAF_LOG_LEVEL.DDWAF_LOG_DEBUG:
                    Log.Debug("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_LOG_INFO:
                    Log.Information("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_LOG_WARN:
                    Log.Warning("{Level}: {Location}: {Message}", level, location, message);
                    break;
                case DDWAF_LOG_LEVEL.DDWAF_LOG_ERROR:
                case DDWAF_LOG_LEVEL.DDWAF_LOG_OFF:
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
