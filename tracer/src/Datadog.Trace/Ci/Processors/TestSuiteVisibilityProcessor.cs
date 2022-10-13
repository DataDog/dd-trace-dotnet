// <copyright file="TestSuiteVisibilityProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;

namespace Datadog.Trace.Ci.Processors;

internal class TestSuiteVisibilityProcessor : ITraceProcessor
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TestSuiteVisibilityProcessor>();
    private readonly bool _isAgentlessEnabled = false;

    public TestSuiteVisibilityProcessor(bool isAgentlessEnabled)
    {
        _isAgentlessEnabled = isAgentlessEnabled;

        Log.Information("TestSuiteVisibilityProcessor initialized.");
    }

    public ArraySegment<Span> Process(ArraySegment<Span> trace)
    {
        // Check if the trace has any span or Agentless is enabled
        if (trace.Count == 0 || _isAgentlessEnabled)
        {
            return trace;
        }

        List<Span> lstSpan = new();
        for (var i = trace.Offset; i < trace.Count + trace.Offset; i++)
        {
            var span = trace.Array![i];
            if (Process(span) is { } processedSpan)
            {
                lstSpan.Add(processedSpan);
            }
            else
            {
                Log.Warning("Span dropped because Test suite visibility is not supported without Agentless [Span.Type={type}]", span.Type);
            }
        }

        return new ArraySegment<Span>(lstSpan.ToArray());
    }

    public Span Process(Span span)
    {
        // If agentless is enabled we don't filter anything.
        if (span is null || _isAgentlessEnabled)
        {
            return span;
        }

        // If we are not in agentless we remove the spans for Suite, Module and Session
        // The reason to do this is because those spans are not supported in the APM intake.
        return span.Type is SpanTypes.TestSuite or SpanTypes.TestModule or SpanTypes.TestSession ? null : span;
    }

    public ITagProcessor GetTagProcessor()
    {
        return null;
    }
}
