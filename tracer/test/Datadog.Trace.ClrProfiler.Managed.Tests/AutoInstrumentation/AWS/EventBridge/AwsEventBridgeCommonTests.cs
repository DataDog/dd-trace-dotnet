// <copyright file="AwsEventBridgeCommonTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Amazon.EventBridge.Model;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AWS.EventBridge;

public class AwsEventBridgeCommonTests
{
    [Fact]
    public void GetCorrectBusName()
    {
        var entries = new List<PutEventsRequestEntry>
        {
            new() { EventBusName = "test-bus-1" },
            new() { EventBusName = "test-bus-2" },
            new() { EventBusName = string.Empty },
            new() { EventBusName = null }
        };

        var result = AwsEventBridgeCommon.GetBusName(entries);
        result.Should().Be("test-bus-1");

        AwsEventBridgeCommon.GetBusName(null).Should().BeNull();
        AwsEventBridgeCommon.GetBusName(new List<PutEventsRequestEntry>()).Should().BeNull();

        var emptyEntries = new List<PutEventsRequestEntry>
        {
            new() { EventBusName = string.Empty },
            new() { EventBusName = null }
        };
        AwsEventBridgeCommon.GetBusName(emptyEntries).Should().BeNull();
    }

    [Fact]
    public void GetCorrectOperationName()
    {
        var tracerV0 = GetTracer("v0");
        AwsEventBridgeCommon.GetOperationName(tracerV0).Should().Be("eventbridge.request");

        var tracerV1 = GetTracer("v1");
        AwsEventBridgeCommon.GetOperationName(tracerV1).Should().Be("aws.eventbridge.send");
    }

    private static Tracer GetTracer(string schemaVersion)
    {
        var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, schemaVersion } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();

        return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
    }
}
