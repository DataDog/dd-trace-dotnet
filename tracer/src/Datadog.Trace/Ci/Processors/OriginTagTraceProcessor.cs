// <copyright file="OriginTagTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;

namespace Datadog.Trace.Ci.Processors
{
    internal sealed class OriginTagTraceProcessor : ITraceProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OriginTagTraceProcessor>();

        private readonly bool _isPartialFlushEnabled = false;
        private readonly bool _isCiVisibilityProtocol = false;
        private DateTimeOffset _warningLastTime = DateTimeOffset.MinValue;
        private int _count = 0;

        public OriginTagTraceProcessor(bool isPartialFlushEnabled, bool isCiVisibilityProtocol)
        {
            _isPartialFlushEnabled = isPartialFlushEnabled;
            _isCiVisibilityProtocol = isCiVisibilityProtocol;

            Log.Debug("OriginTraceProcessor initialized.");
        }

        public SpanCollection Process(in SpanCollection trace)
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
                foreach (var span in trace)
                {
                    if (span.Context.Parent is null &&
                        span.Type != SpanTypes.Test &&
                        span.Type != SpanTypes.Browser &&
                        span.Type != SpanTypes.TestSuite &&
                        span.Type != SpanTypes.TestModule &&
                        span.Type != SpanTypes.TestSession &&
                        span.Type != SpanTypes.Benchmark &&
                        span.Type != SpanTypes.Build)
                    {
                        if (TraceClock.Instance.UtcNow - _warningLastTime > TimeSpan.FromSeconds(1))
                        {
                            _warningLastTime = TraceClock.Instance.UtcNow;
                            Log.Warning<int>("Spans dropped because not having a test or benchmark root span: {Count}", trace.Count + Interlocked.Exchange(ref _count, 0));
                        }
                        else
                        {
                            Interlocked.Increment(ref _count);
                        }

                        return default;
                    }
                }
            }

            if (!_isCiVisibilityProtocol)
            {
                // Sets the origin tag on the TraceContext to ensure the CI track.
                var traceContext = trace.FirstSpan?.Context.TraceContext;

                if (traceContext is not null)
                {
                    traceContext.Origin = TestTags.CIAppTestOriginName;
                }
            }

            return trace;
        }

        public Span? Process(Span? span)
        {
            if (span is null)
            {
                return span;
            }

            // Sets the origin tag on the TraceContext to ensure the CI track.
            var traceContext = span.Context.TraceContext;

            if (traceContext is not null)
            {
                traceContext.Origin = TestTags.CIAppTestOriginName;
            }

            return span;
        }

        public ITagProcessor? GetTagProcessor()
        {
            return null;
        }
    }
}
