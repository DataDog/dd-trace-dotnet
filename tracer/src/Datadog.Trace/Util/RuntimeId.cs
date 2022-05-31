// <copyright file="RuntimeId.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Util
{
    internal static class RuntimeId
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;

        private static bool IsNativeLoaderAvailable
        {
            get
            {
                var fd = FrameworkDescription.Instance;
                return fd.ProcessArchitecture != ProcessArchitecture.Arm && fd.ProcessArchitecture != ProcessArchitecture.Arm64;
            }
        }

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetImpl());

        private static string GetImpl()
        {
            if (IsNativeLoaderAvailable && TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native: " + runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Debug("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : {NewGuid}", guid);

            return guid;
        }

        // Adding the attribute MethodImpl(MethodImplOptions.NoInlining) allows the caller to
        // catch the SecurityException in case of we are running in a partial trust environment.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IntPtr GetRuntimeIdFromNative()
        {
            return NativeMethods.GetRuntimeIdFromNative();
        }

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
                // We failed to retrieve the runtime from native this can be because:
                // - P/Invoke issue (unknown dll, unknown entrypoint...)
                // - We are running in a partial trust environment
                Log.Information("Failed to get the runtime-id from native: {Reason}", e.Message);
            }

            runtimeId = default;
            return false;
        }

        private static class NativeMethods
        {
            [DllImport("Datadog.AutoInstrumentation.NativeLoader", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
            public static extern IntPtr GetRuntimeIdFromNative();
        }
    }
}
