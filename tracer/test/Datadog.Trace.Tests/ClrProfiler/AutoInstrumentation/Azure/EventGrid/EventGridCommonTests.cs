// <copyright file="EventGridCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

public class EventGridCommonTests
{
    [Theory]
    [InlineData("Datadog", false)]
    [InlineData("baggage", false)]
    [InlineData("tracecontext", true)]
    [InlineData("W3C", true)]
    public async Task InjectW3CContextHonorsConfiguredPropagationStyles(string propagationStyle, bool shouldInjectTraceContext)
    {
        await using var tracer = TracerHelper.Create();
        using var scope = tracer.StartActiveInternal("event-grid-test");
        var extensionAttributes = new Dictionary<string, object>
        {
            ["traceparent"] = "existing-traceparent",
            ["tracestate"] = "existing-tracestate",
        };

        EventGridCommon.InjectW3CContext(extensionAttributes, scope, [propagationStyle]);

        if (shouldInjectTraceContext)
        {
            extensionAttributes["traceparent"].Should().NotBe("existing-traceparent");
            extensionAttributes["tracestate"].Should().NotBe("existing-tracestate");
        }
        else
        {
            extensionAttributes["traceparent"].Should().Be("existing-traceparent");
            extensionAttributes["tracestate"].Should().Be("existing-tracestate");
        }
    }
}
