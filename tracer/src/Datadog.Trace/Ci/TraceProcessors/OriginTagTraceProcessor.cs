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
        private readonly bool _isAgentlessEnabled = false;

        public OriginTagTraceProcessor(bool isPartialFlushEnabled, bool isAgentlessEnabled)
        {
            _isPartialFlushEnabled = isPartialFlushEnabled;
            _isAgentlessEnabled = isAgentlessEnabled;

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
                // Check if the root span is a test, benchmark or build span
                for (var i = trace.Offset + trace.Count - 1; i >= trace.Offset; i--)
                {
                    var span = trace.Array![i];
                    if (span.Context.Parent is null &&
                        span.Type != SpanTypes.Test &&
                        span.Type != SpanTypes.Benchmark &&
                        span.Type != SpanTypes.Build)
                    {
                        Log.Warning<int>("Spans dropped because not having a test or benchmark root span: {Count}", trace.Count);
                        return default;
                    }
                }
            }

            if (!_isAgentlessEnabled)
            {
                for (var i = trace.Offset; i < trace.Count + trace.Offset; i++)
                {
                    trace.Array[i] = Process(trace.Array[i]);
                }
            }

            return trace;
        }

        public Span Process(Span span)
        {
            // Sets the origin tag to any other spans to ensure the CI track.
            span.Context.Origin = TestTags.CIAppTestOriginName;
            return span;
        }
    }
}
