// <copyright file="TestSuiteVisibilityProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;

namespace Datadog.Trace.Ci.Processors;

internal class TestSuiteVisibilityProcessor : ITraceProcessor
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestSuiteVisibilityProcessor>();

    public TestSuiteVisibilityProcessor()
    {
        Log.Information("TestSuiteVisibilityProcessor initialized.");
    }

    public SpanCollection Process(in SpanCollection trace)
    {
        var originalCount = trace.Count;
        if (originalCount == 0)
        {
            return trace;
        }

        // Check if the trace has any span or Agentless is enabled

        // special case single span case
        if (originalCount == 1)
        {
            var span = trace[0];
            if (Process(span) is null)
            {
                Log.Warning("Span dropped because Test suite visibility is not supported without Agentless [Span.Type={Type}]", span.Type);
                return default;
            }

            return trace;
        }

        // we know we have multiple spans, so get the underlying array
        var segment = trace.ToArray();
        Span[]? spans = null;
        var copiedCount = 0;
        var haveDrops = false;
        // TODO: we could reuse the same underlying array rather than re-allocating when we need to recreate, but that can be a separate optimization
        for (var i = segment.Offset; i < segment.Count + segment.Offset; i++)
        {
            var span = segment.Array![i];
            if (Process(span) is { } processedSpan)
            {
                if (haveDrops)
                {
                    if (spans is null)
                    {
                        // first kept span after dropping some spans
                        spans = new Span[trace.Count];
                        Array.Copy(segment.Array!, segment.Offset, spans, destinationIndex: 0, length: i - segment.Offset);
                    }

                    spans[copiedCount++] = processedSpan;
                }
            }
            else
            {
                haveDrops = true;
                Log.Warning("Span dropped because Test suite visibility is not supported without Agentless [Span.Type={Type}]", span.Type);
            }
        }

        return haveDrops
                   ? spans is null ? default : new SpanCollection(spans, copiedCount)
                   : trace;
    }

    public Span? Process(Span? span)
    {
        // If agentless is enabled we don't filter anything.
        if (span is null)
        {
            return span;
        }

        // If we are not in agentless we remove the spans for Suite, Module and Session
        // The reason to do this is because those spans are not supported in the APM intake.
        return span.Type is SpanTypes.TestSuite or SpanTypes.TestModule or SpanTypes.TestSession ? null : span;
    }

    public ITagProcessor? GetTagProcessor()
    {
        return null;
    }
}
