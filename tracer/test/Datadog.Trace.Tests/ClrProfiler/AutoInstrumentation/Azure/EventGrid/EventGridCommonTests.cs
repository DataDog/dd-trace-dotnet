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
    [InlineData("Datadog", false, false)]
    [InlineData("baggage", false, true)]
    [InlineData("tracecontext", true, false)]
    [InlineData("W3C", true, false)]
    public async Task InjectW3CContextHonorsConfiguredPropagationStyles(string propagationStyle, bool shouldInjectTraceContext, bool shouldInjectBaggage)
    {
        await using var tracer = TracerHelper.Create();
        using var scope = tracer.StartActiveInternal("event-grid-test");
        var extensionAttributes = new Dictionary<string, object>
        {
            ["traceparent"] = "existing-traceparent",
            ["tracestate"] = "existing-tracestate",
            ["baggage"] = "existing-baggage",
        };

        var originalBaggage = Baggage.Current;
        Baggage.Current = new Baggage { ["test-key"] = "test-value" };

        try
        {
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

            extensionAttributes["baggage"].Should().Be(shouldInjectBaggage ? "test-key=test-value" : "existing-baggage");
        }
        finally
        {
            Baggage.Current = originalBaggage;
        }
    }
}
