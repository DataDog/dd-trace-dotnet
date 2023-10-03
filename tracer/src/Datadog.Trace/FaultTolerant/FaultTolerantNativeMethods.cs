// <copyright file="FaultTolerantNativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink.Models;

namespace Datadog.Trace.FaultTolerant
{
    // ReSharper disable MemberHidesStaticFromOuterClass
    internal static class FaultTolerantNativeMethods
    {
        private static readonly bool IsWindows = FrameworkDescription.Instance.IsWindows();

        public static bool ShouldHeal(IntPtr moduleId, int methodToken, string instrumentationId, int products)
        {
            if (IsWindows)
            {
                return Windows.ShouldHeal(moduleId, methodToken, instrumentationId, products);
            }
            else
            {
                return NonWindows.ShouldHeal(moduleId, methodToken, instrumentationId, products);
            }
        }

        public static void ReportSuccessfulInstrumentation(IntPtr moduleId, int methodToken, string instrumentationId, int products)
        {
            if (IsWindows)
            {
                Windows.ReportSuccessfulInstrumentation(moduleId, methodToken, instrumentationId, products);
            }
            else
            {
                NonWindows.ReportSuccessfulInstrumentation(moduleId, methodToken, instrumentationId, products);
            }
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        // These methods are rewritten by the native tracer to use the correct paths
        private static class Windows
        {
            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern bool ShouldHeal(
                [In] IntPtr moduleId,
                [In] int methodToken,
                [In] [MarshalAs(UnmanagedType.LPWStr)] string instrumentationId,
                [In] int products);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void ReportSuccessfulInstrumentation(
                [In] IntPtr moduleId,
                [In] int methodToken,
                [In] [MarshalAs(UnmanagedType.LPWStr)] string instrumentationId,
                [In] int products);
        }

        // assume .NET Core if not running on Windows
        // These methods are rewritten by the native tracer to use the correct paths
        private static class NonWindows
        {
            [DllImport("Datadog.Tracer.Native")]
            public static extern bool ShouldHeal(
                [In] IntPtr moduleId,
                [In] int methodToken,
                [In] [MarshalAs(UnmanagedType.LPWStr)] string instrumentationId,
                [In] int products);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void ReportSuccessfulInstrumentation(
                [In] IntPtr moduleId,
                [In] int methodToken,
                [In][MarshalAs(UnmanagedType.LPWStr)] string instrumentationId,
                [In] int products);
        }
    }
}
