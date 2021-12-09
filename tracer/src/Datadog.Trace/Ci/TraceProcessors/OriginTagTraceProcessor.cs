// <copyright file="OriginTagTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.TraceProcessors;

namespace Datadog.Trace.Ci.TraceProcessors
{
    internal class OriginTagTraceProcessor : ITraceProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TruncatorTraceProcessor>();

        private readonly bool _isPartialFlushEnabled = false;

        public OriginTagTraceProcessor(bool isPartialFlushEnabled)
        {
            _isPartialFlushEnabled = isPartialFlushEnabled;

            Log.Information("OriginTraceProcessor initialized.");
        }

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            // We ensure there's no trace (local root span) without a test tag.
            // And ensure all other spans have the origin tag.

            // Check if the trace has any span
            if (trace.Count == 0)
            {
                // No trace to write
                return trace;
            }

            if (!_isPartialFlushEnabled)
            {
                // Check if the last span (the root) is a test, bechmark or build span
                Span lastSpan = trace.Array[trace.Offset + trace.Count - 1];
                if (lastSpan.Context.Parent is null &&
                    lastSpan.Type != SpanTypes.Test &&
                    lastSpan.Type != SpanTypes.Benchmark &&
                    lastSpan.Type != SpanTypes.Build)
                {
                    Log.Warning<int>("Spans dropped because not having a test or benchmark root span: {Count}", trace.Count);
                    return default;
                }
            }

            foreach (var span in trace)
            {
                // Sets the origin tag to any other spans to ensure the CI track.
                span.Context.Origin = TestTags.CIAppTestOriginName;
            }

            return trace;
        }
    }
}
