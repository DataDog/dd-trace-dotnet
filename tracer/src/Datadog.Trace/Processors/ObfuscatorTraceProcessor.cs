// <copyright file="ObfuscatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.TraceProcessors
{
    internal class ObfuscatorTraceProcessor : ITraceProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ObfuscatorTraceProcessor>();

        public ObfuscatorTraceProcessor()
        {
            Log.Information("ObfuscatorTraceProcessor initialized.");
        }

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            for (var i = trace.Offset; i < trace.Count + trace.Offset; i++)
            {
                trace.Array[i] = Process(trace.Array[i]);
            }

            return trace;
        }

        public Span Process(Span span)
        {
            if (span.Type == "sql" || span.Type == "cassandra")
            {
                span.ResourceName = ObfuscateSqlResource(span.ResourceName);
            }

            return span;
        }

        internal static string ObfuscateSqlResource(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return string.Empty;
            }

            return Obfuscator.SqlObfuscator(query);
        }
    }
}
