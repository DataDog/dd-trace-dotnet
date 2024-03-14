// <copyright file="ExporterBindings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable MemberHidesStaticFromOuterClass
namespace Datadog.Trace.Agent.Native
{
    internal static class ExporterBindings
    {
        private static readonly bool IsWindows = FrameworkDescription.Instance.IsWindows();

        public static bool TryInitializeExporter(string host, int port, string containerId, string language, string languageVersion, string languageInterpreter,  string entityId, string tracerVersion)
        {
            if (IsWindows)
            {
                return ExporterWindows.InitializeExporter(host, port, containerId, language, languageVersion, languageInterpreter, entityId, tracerVersion);
            }

            return ExporterNonWindows.InitializeExporter(host, port, containerId, language, languageVersion, languageInterpreter, entityId, tracerVersion);
        }

        public static void SendTrace(byte[] buffer, int traceCount)
        {
            if (IsWindows)
            {
                ExporterWindows.SendTrace(buffer, traceCount);
            }

            ExporterNonWindows.SendTrace(buffer, traceCount);
        }

        public static void SetSamplingRateCallback(Action<Dictionary<string, float>> updateSampleRates)
        {
            // if (IsWindows)
            // {
            //     ExporterWindows.SetSamplingRateCallback(url, containerId, language, languageVersion, languageInterpreter, entityId, tracerVersion);
            // }
            //
            // ExporterNonWindows.SetSamplingRateCallback(url, containerId, language, languageVersion, languageInterpreter, entityId, tracerVersion);
        }

        // the "dll" extension is required on .NET Framework
        // and optional on .NET Core
        // The DllImport methods are re-written by cor_profiler to have the correct vales
        private static class ExporterWindows
        {
            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern bool InitializeExporter(
                [MarshalAs(UnmanagedType.LPWStr)] string host,
                int port,
                [MarshalAs(UnmanagedType.LPWStr)] string containerId,
                [MarshalAs(UnmanagedType.LPWStr)] string language,
                [MarshalAs(UnmanagedType.LPWStr)] string languageVersion,
                [MarshalAs(UnmanagedType.LPWStr)] string languageInterpreter,
                [MarshalAs(UnmanagedType.LPWStr)] string entityId,
                [MarshalAs(UnmanagedType.LPWStr)] string tracerVersion);

            [DllImport("Datadog.Tracer.Native.dll")]
            public static extern void SendTrace([MarshalAs(UnmanagedType.LPArray)] byte[] buffer, int nbTrace);
        }

        // assume .NET Core if not running on Windows
        // The DllImport methods are re-written by cor_profiler to have the correct vales
        private static class ExporterNonWindows
        {
            [DllImport("Datadog.Tracer.Native")]
            public static extern bool InitializeExporter(
                [MarshalAs(UnmanagedType.LPWStr)] string host,
                int port,
                [MarshalAs(UnmanagedType.LPWStr)] string containerId,
                [MarshalAs(UnmanagedType.LPWStr)] string language,
                [MarshalAs(UnmanagedType.LPWStr)] string languageVersion,
                [MarshalAs(UnmanagedType.LPWStr)] string languageInterpreter,
                [MarshalAs(UnmanagedType.LPWStr)] string entityId,
                [MarshalAs(UnmanagedType.LPWStr)] string tracerVersion);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void SendTrace([MarshalAs(UnmanagedType.LPArray)] byte[] buffer, int nbTrace);
        }
    }
}
