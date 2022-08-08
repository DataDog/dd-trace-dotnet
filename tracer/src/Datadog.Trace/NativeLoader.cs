// <copyright file="NativeLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal static class NativeLoader
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NativeLoader));

        private static bool IsAvailable
        {
            get
            {
                var fd = FrameworkDescription.Instance;
                return fd.ProcessArchitecture != ProcessArchitecture.Arm && fd.ProcessArchitecture != ProcessArchitecture.Arm64;
            }
        }

        public static bool TryGetRuntimeIdFromNative(out string runtimeId)
        {
            if (!IsAvailable)
            {
                runtimeId = default;
                return false;
            }

            try
            {
                var runtimeIdPtr = GetRuntimeId();
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

        // Adding the attribute MethodImpl(MethodImplOptions.NoInlining) allows the caller to
        // catch the SecurityException in case of we are running in a partial trust environment.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IntPtr GetRuntimeId()
        {
            return NativeMethods.GetRuntimeIdFromNative();
        }

        private static class NativeMethods
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
            public static extern IntPtr GetRuntimeIdFromNative();
        }
    }
}
