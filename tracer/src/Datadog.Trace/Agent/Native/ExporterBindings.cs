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
        public static bool TryInitializeExporter(string host, int port, string tracerVersion, string language, string languageVersion, string languageInterpreter)
        {
            return NativeMethods.InitializeExporter(host, port, tracerVersion, language, languageVersion, languageInterpreter);
        }

        public static void SendTrace(byte[] buffer, int traceCount)
        {
            NativeMethods.SendTrace(buffer, buffer.Length, traceCount);
        }

        public static void CreateStatsExporter(string hostname, string env, string version, string lang, string tracerVersion, string runtimeId, string service, string containerId, string gitCommitSha, string[] tags, string agentUrl)
        {
            NativeMethods.CreateStatsExporter(hostname, env, version, lang, tracerVersion, runtimeId, service, containerId, gitCommitSha, tags, tags.Length, agentUrl);
        }

        public static void AddSpanToBucket(string resourceName, string serviceName, string operationName, string spanType, int httpStatusCode, bool isSyntheticsRequest, bool isTopLevel, bool isError, long duration)
        {
            NativeMethods.AddSpanToBucket(resourceName, serviceName, operationName, spanType, httpStatusCode, isSyntheticsRequest, isTopLevel, isError, duration);
        }

        public static void FlushStats(long bucketDurationNs)
        {
            NativeMethods.FlushStats(bucketDurationNs);
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
            [DllImport("Datadog.Tracer.Native", EntryPoint = "InitializeTraceExporter")]
            public static extern bool InitializeExporter(
                string host,
                int port,
                string tracerVersion,
                string language,
                string languageVersion,
                string languageInterpreter);

            [DllImport("Datadog.Tracer.Native", EntryPoint = "SendTrace")]
            public static extern void SendTrace(byte[] buffer, int bufferSize, int nbTrace);

            [DllImport("Datadog.Tracer.Native", EntryPoint = "InitializeStatsExporter")]
            public static extern void CreateStatsExporter(
                string hostname,
                string env,
                string version,
                string lang,
                string tracerVersion,
                string runtimeId,
                string service,
                string containerId,
                string gitCommitSha,
                string[] tags,
                long nbTags,
                string agentUrl);

            [DllImport("Datadog.Tracer.Native", EntryPoint = "AddSpanToBucket")]
            public static extern void AddSpanToBucket(string resourceName, string serviceName, string operationName, string spanType, int httpStatusCode, bool isSyntheticsRequest, bool isTopLevel, bool isError, long duration);

            [DllImport("Datadog.Tracer.Native", EntryPoint = "FlushStats")]
            public static extern void FlushStats(long bucketDurationNs);
        }
    }
}
