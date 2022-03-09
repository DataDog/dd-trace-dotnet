// <copyright file="RuntimeId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    internal static class RuntimeId
    {
#if NETFRAMEWORK
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader.dll";
#else
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;

        /// <summary>
        /// This method must be called in case we want to share the runtime id between profiler, debugger, tracer...
        /// /!\ This method must not be called in manual instrumentation use case, because we could run in application
        /// with Partial Trust and we won't be able to P/Invoke (and may crash the application)
        /// </summary>
        public static void InitializeFromNative()
        {
             _runtimeId = GetFromNative();
        }

        /// <summary>
        /// In case of automatic instrumentation, this method will return the runtime id computed by InitializeFromNative method.
        /// In case of manual instrumentation, this method will create the guid (once and for all).
        /// </summary>
        /// <returns>runtime id (GUUID)</returns>
        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => Guid.NewGuid().ToString());

        private static string GetFromNative()
        {
            if (TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native: " + runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Information("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : {NewGuid}", guid);

            return guid;
        }

        [DllImport(NativeLoaderLibName, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern IntPtr GetRuntimeIdFromNative();

        private static bool TryGetRuntimeIdFromNative(out string runtimeId)
        {
            try
            {
                var runtimeIdPtr = GetRuntimeIdFromNative();
                runtimeId = Marshal.PtrToStringAnsi(runtimeIdPtr);
                return !string.IsNullOrWhiteSpace(runtimeId);
            }
            catch (Exception e)
            {
                Log.Warning("Failed to get the runtime-id from native: {Reason}", e.Message);
            }

            runtimeId = default;
            return false;
        }
    }
}
