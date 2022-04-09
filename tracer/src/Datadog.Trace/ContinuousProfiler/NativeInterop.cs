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
            if (Environment.Is64BitProcess)
            {
                return GetProfilerStatusPointer_x64();
            }

            return GetProfilerStatusPointer_x86();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static IntPtr GetTraceContextNativePointer()
        {
            if (Environment.Is64BitProcess)
            {
                return GetTraceContextNativePointer_x64();
            }

            return GetTraceContextNativePointer_x86();
        }

        [DllImport(Constants.NativeProfilerLibNameX64, EntryPoint = "GetNativeProfilerIsReadyPtr")]
        private static extern IntPtr GetProfilerStatusPointer_x64();

        [DllImport(Constants.NativeProfilerLibNameX86, EntryPoint = "GetNativeProfilerIsReadyPtr")]
        private static extern IntPtr GetProfilerStatusPointer_x86();

        [DllImport(dllName: Constants.NativeProfilerLibNameX64, EntryPoint = "GetPointerToNativeTraceContext")]
        private static extern IntPtr GetTraceContextNativePointer_x64();

        [DllImport(dllName: Constants.NativeProfilerLibNameX86, EntryPoint = "GetPointerToNativeTraceContext")]
        private static extern IntPtr GetTraceContextNativePointer_x86();
    }
}
