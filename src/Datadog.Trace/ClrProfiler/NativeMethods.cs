// <copyright file="NativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace Datadog.Trace.ClrProfiler
{
    internal static class NativeMethods
    {
        private static readonly bool IsWindows = string.Equals(FrameworkDescription.Instance.OSPlatform, "Windows", StringComparison.OrdinalIgnoreCase);

        public static bool IsProfilerAttached()
        {
            if (IsWindows)
            {
                return Windows.IsProfilerAttached();
            }

            return NonWindows.IsProfilerAttached();
        }

        public static void InitializeProfiler(NativeCallTargetDefinition[] methodArrays)
        {
            if (IsWindows)
            {
                Windows.InitializeProfiler(methodArrays, methodArrays.Length);
            }
            else
            {
                NonWindows.InitializeProfiler(methodArrays, methodArrays.Length);
            }
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        private static class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void InitializeProfiler([In, Out] NativeCallTargetDefinition[] methodArrays, int size);
        }

        // assume .NET Core if not running on Windows
        private static class NonWindows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern bool IsProfilerAttached();

            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void InitializeProfiler([In, Out] NativeCallTargetDefinition[] methodArrays, int size);
        }
    }
}
