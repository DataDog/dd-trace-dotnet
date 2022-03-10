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
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader.dll";
#else
        private const string NativeLoaderLibName = "Datadog.AutoInstrumentation.NativeLoader";
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeId));
        private static string _runtimeId;

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetImpl());

        private static string GetImpl()
        {
            try
            {
                if (TryGetRuntimeIdFromNative(out var runtimeId))
                {
                    Log.Information("Runtime id retrieved from native: " + runtimeId);
                    return runtimeId;
                }
            }
            catch (Exception)
            {
                // We were unable to get the runtime id from native. In this case, we might be running in a partial trust environment
                // Just catch the exception, compute a new guid and return it.
            }

            var guid = Guid.NewGuid().ToString();
            Log.Information("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : {NewGuid}", guid);

            return guid;
        }

        [DllImport(NativeLoaderLibName, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern IntPtr GetRuntimeIdFromNative();

        // Adding the attribute MethodImpl(MethodImplOptions.NoInlining) allows the caller to
        // catch the SecurityException in case of we are running in a partial trust environment.
        [MethodImpl(MethodImplOptions.NoInlining)]
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
