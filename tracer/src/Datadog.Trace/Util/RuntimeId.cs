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
#if NETFRAMEWORK
        private const string NativeLoaderLibNameX86 = "Datadog.AutoInstrumentation.NativeLoader.x86.dll";
        private const string NativeLoaderLibNameX64 = "Datadog.AutoInstrumentation.NativeLoader.x64.dll";
#else
        private const string NativeLoaderLibNameX86 = "Datadog.AutoInstrumentation.NativeLoader.x86";
        private const string NativeLoaderLibNameX64 = "Datadog.AutoInstrumentation.NativeLoader.x64";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetImpl());

        private static string GetImpl()
        {
            if (TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native: " + runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Debug("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : {NewGuid}", guid);

            return guid;
        }

        [DllImport(NativeLoaderLibNameX86, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern IntPtr GetRuntimeIdFromNativeX86();

        [DllImport(NativeLoaderLibNameX64, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern IntPtr GetRuntimeIdFromNativeX64();

        // Adding the attribute MethodImpl(MethodImplOptions.NoInlining) allows the caller to
        // catch the SecurityException in case of we are running in a partial trust environment.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IntPtr GetRuntimeIdFromNative()
        {
            if (Environment.Is64BitProcess)
            {
                return GetRuntimeIdFromNativeX64();
            }

            return GetRuntimeIdFromNativeX86();
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
                Log.Warning("Failed to get the runtime-id from native: {Reason}", e.Message);
            }

            runtimeId = default;
            return false;
        }
    }
}
