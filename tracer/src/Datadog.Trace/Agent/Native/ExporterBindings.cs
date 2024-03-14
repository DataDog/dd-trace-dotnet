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
        public static bool TryInitializeExporter(string host, int port, string tracer_version, string language, string languageVersion, string languageInterpreter)
        {
            return NativeMethods.InitializeExporter(host, port, tracer_version, language, languageVersion, languageInterpreter);
        }

        public static void SendTrace(byte[] buffer, int traceCount)
        {
            NativeMethods.SendTrace(buffer, buffer.Length, traceCount);
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

        private static class NativeMethods
        {
            [DllImport("Datadog.Tracer.Native")]
            public static extern bool InitializeExporter(
                string host,
                int port,
                string tracerVersion,
                string language,
                string languageVersion,
                string languageInterpreter);

            [DllImport("Datadog.Tracer.Native")]
            public static extern void SendTrace(byte[] buffer, int buffer_size, int nbTrace);
        }
    }
}
