// <copyright file="DebuggerNativeMethods.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.Debugger.ProbeStatuses;

namespace Datadog.Trace.Debugger.PInvoke
{
    internal static class DebuggerNativeMethods
    {
        public static void InstrumentProbes(NativeMethodProbeDefinition[] methodProbes, NativeLineProbeDefinition[] lineProbes, NativeRemoveProbeRequest[] revertProbes)
        {
            if (FrameworkDescription.Instance.IsWindows())
            {
                Windows.InstrumentProbes(methodProbes, methodProbes.Length, lineProbes, lineProbes.Length, revertProbes, revertProbes.Length);
            }
            else
            {
                NonWindows.InstrumentProbes(methodProbes, methodProbes.Length, lineProbes, lineProbes.Length, revertProbes, revertProbes.Length);
            }
        }

        public static ProbeStatus[] GetProbesStatuses(string[] probeIds)
        {
            if (probeIds is null || probeIds.Length == 0)
            {
                return Array.Empty<ProbeStatus>();
            }

            var probesStatuses = new NativeProbeStatus[probeIds.Length];
            int probesLength = FrameworkDescription.Instance.IsWindows() ?
                                    Windows.GetProbesStatuses(probeIds, probeIds.Length, probesStatuses) :
                                    NonWindows.GetProbesStatuses(probeIds, probeIds.Length, probesStatuses);

            if (probesLength == 0)
            {
                return Array.Empty<ProbeStatus>();
            }

            return probesStatuses.Take(probesLength)
                                 .Select(
                                      nativeProbeStatus =>
                                          new ProbeStatus(
                                              Marshal.PtrToStringUni(nativeProbeStatus.ProbeId), nativeProbeStatus.Status))
                                 .ToArray();
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        // These methods are rewritten by the native tracer to use the correct paths
        private static partial class Windows
        {
            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void InstrumentProbes(
                [In] NativeMethodProbeDefinition[] methodProbes,
                int methodProbesLength,
                [In] NativeLineProbeDefinition[] lineProbes,
                int lineProbesLength,
                [In] NativeRemoveProbeRequest[] revertProbes,
                int revertProbesLength);

            [DllImport("Datadog.Tracer.Native.dll", CharSet = CharSet.Unicode)]
            public static extern int GetProbesStatuses(
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] probeIds,
                int probeIdsLength,
                [In, Out] NativeProbeStatus[] probeStatuses);
        }

        // assume .NET Core if not running on Windows
        // These methods are rewritten by the native tracer to use the correct paths
        private static partial class NonWindows
        {
            [DllImport("Datadog.Tracer.Native")]
            public static extern void InstrumentProbes(
                [In] NativeMethodProbeDefinition[] methodProbes,
                int methodProbesLength,
                [In] NativeLineProbeDefinition[] lineProbes,
                int lineProbesLength,
                [In] NativeRemoveProbeRequest[] revertProbes,
                int revertProbesLength);

            [DllImport("Datadog.Tracer.Native")]
            public static extern int GetProbesStatuses(
                [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] probeIds,
                int probeIdsLength,
                [In, Out] NativeProbeStatus[] probeStatuses);
        }
    }
}
