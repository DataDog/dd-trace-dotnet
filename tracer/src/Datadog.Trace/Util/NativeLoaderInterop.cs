// <copyright file="NativeLoaderInterop.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Util
{
    internal class NativeLoaderInterop
    {
#if NETFRAMEWORK
        private const string NativeLoaderLibNameX86 = "Datadog.AutoInstrumentation.NativeLoader.x86.dll";
        private const string NativeLoaderLibNameX64 = "Datadog.AutoInstrumentation.NativeLoader.x64.dll";
#else
        private const string NativeLoaderLibNameX86 = "Datadog.AutoInstrumentation.NativeLoader.x86";
        private const string NativeLoaderLibNameX64 = "Datadog.AutoInstrumentation.NativeLoader.x64";
#endif

        // Adding the attribute MethodImpl(MethodImplOptions.NoInlining) allows the caller to
        // catch the SecurityException in case of we are running in a partial trust environment.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string GetRuntimeIdFromNative()
        {
            if (Environment.Is64BitProcess)
            {
                return GetRuntimeIdFromNativeX64();
            }

            return GetRuntimeIdFromNativeX86();
        }

        [DllImport(NativeLoaderLibNameX86, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern string GetRuntimeIdFromNativeX86();

        [DllImport(NativeLoaderLibNameX64, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentAppDomainRuntimeId")]
        private static extern string GetRuntimeIdFromNativeX64();
    }
}
