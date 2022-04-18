// <copyright file="DebuggerNativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.PInvoke;

namespace Datadog.Trace.Debugger.PInvoke
{
    internal static class DebuggerNativeMethods
    {
        public static void InstrumentProbes(NativeMethodProbeDefinition[] methodProbes, NativeLineProbeDefinition[] lineProbes, NativeRemoveProbeRequest[] revertProbes)
        {
            if (Datadog.Trace.ClrProfiler.NativeMethods.IsWindows)
            {
                Windows.InstrumentProbes(methodProbes, methodProbes.Length, lineProbes, lineProbes.Length, revertProbes, revertProbes.Length);
            }
            else
            {
                NonWindows.InstrumentProbes(methodProbes, methodProbes.Length, lineProbes, lineProbes.Length, revertProbes, revertProbes.Length);
            }
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        private static partial class Windows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native.dll")]
            public static extern void InstrumentProbes(
                [In] NativeMethodProbeDefinition[] methodProbes,
                int methodProbesLength,
                [In] NativeLineProbeDefinition[] lineProbes,
                int lineProbesLength,
                [In] NativeRemoveProbeRequest[] revertProbes,
                int revertProbesLength);
        }

        // assume .NET Core if not running on Windows
        private static partial class NonWindows
        {
            [DllImport("Datadog.Trace.ClrProfiler.Native")]
            public static extern void InstrumentProbes(
                [In] NativeMethodProbeDefinition[] methodProbes,
                int methodProbesLength,
                [In] NativeLineProbeDefinition[] lineProbes,
                int lineProbesLength,
                [In] NativeRemoveProbeRequest[] revertProbes,
                int revertProbesLength);
        }
    }
}
