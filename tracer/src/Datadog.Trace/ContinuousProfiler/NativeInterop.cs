// <copyright file="NativeInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ContinuousProfiler
{
    internal class NativeInterop
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetProfilerStatusPointer()
        {
            return NativeMethods.GetProfilerStatusPointer();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetTraceContextNativePointer()
        {
            return NativeMethods.GetTraceContextNativePointer();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetApplicationInfoForAppDomain(string runtimeId, string serviceName, string environment, string version)
        {
            NativeMethods.SetApplicationInfoForAppDomain(runtimeId, serviceName, environment, version);
        }

        // These methods are rewritten by the native tracer to use the correct paths
        private static class NativeMethods
        {
            [DllImport(dllName: "Datadog.Profiler.Native", EntryPoint = "GetNativeProfilerIsReadyPtr")]
            public static extern IntPtr GetProfilerStatusPointer();

            [DllImport(dllName: "Datadog.Profiler.Native", EntryPoint = "GetPointerToNativeTraceContext")]
            public static extern IntPtr GetTraceContextNativePointer();

            [DllImport(dllName: "Datadog.Profiler.Native", EntryPoint = "SetApplicationInfoForAppDomain")]
            public static extern void SetApplicationInfoForAppDomain(string runtimeId, string serviceName, string environment, string version);
        }
    }
}
