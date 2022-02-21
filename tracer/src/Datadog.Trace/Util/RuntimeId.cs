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

        public static string Get() => LazyInitializer.EnsureInitialized(ref _runtimeId, () => GetImpl());

        private static string GetImpl()
        {
            if (TryGetRuntimeIdFromNative(out var runtimeId))
            {
                Log.Information("Runtime id retrieved from native: " + runtimeId);
                return runtimeId;
            }

            var guid = Guid.NewGuid().ToString();
            Log.Information("Unable to get the runtime id from native. Fallback to Guid.NewGuid() : " + guid);

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
                Log.Information(e, "An exception occured while retrieving the runtime-id from native.");
            }

            runtimeId = default;
            return false;
        }
    }
}
