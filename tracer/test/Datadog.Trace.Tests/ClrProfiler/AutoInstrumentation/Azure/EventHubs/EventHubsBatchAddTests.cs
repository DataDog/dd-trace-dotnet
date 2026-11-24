// <copyright file="EventHubsBatchAddTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.EventHubs;

public class EventHubsBatchAddTests
{
    private const string PathwayHeader = "dd-pathway-ctx-base64";

    [Fact]
    public async Task RetryIntoTheSameHubReusesThePathwayAndTagsTheRetrySpan()
    {
        await using var tracer = CreateTracer();
        var eventData = CreateEventData();

        var (firstHash, firstHeader) = AddToBatch(tracer, eventData, "hub-a", accepted: false);
        var (retryHash, retryHeader) = AddToBatch(tracer, eventData, "hub-a", accepted: true);

        firstHash.Should().NotBeNullOrEmpty();
        retryHash.Should().Be(firstHash);
        retryHeader.Should().Be(firstHeader);
    }

    [Fact]
    public async Task RetryIntoAnotherHubSetsANewCheckpoint()
    {
        await using var tracer = CreateTracer();
        var eventData = CreateEventData();

        var (firstHash, firstHeader) = AddToBatch(tracer, eventData, "hub-a", accepted: false);
        var (retryHash, retryHeader) = AddToBatch(tracer, eventData, "hub-b", accepted: true);

        firstHash.Should().NotBeNullOrEmpty();
        retryHash.Should().NotBeNullOrEmpty().And.NotBe(firstHash);
        retryHeader.Should().NotBe(firstHeader);
    }

    private static (string? Hash, object? Header) AddToBatch(Tracer tracer, Mock<IEventData> eventData, string hub, bool accepted)
    {
        using var scope = tracer.StartActiveInternal("create");
        EventHubsCommon.SetBatchAddProduceCheckpoint(tracer, eventData.Object, hub);
        EventHubsCommon.CompleteBatchAdd(eventData.Object.Instance!, accepted);
        eventData.Object.Properties.TryGetValue(PathwayHeader, out var header);
        return (scope.Span.GetTag("pathway.hash"), header);
    }

    private static Mock<IEventData> CreateEventData()
    {
        var properties = new Dictionary<string, object>();
        var eventData = new Mock<IEventData>();
        eventData.Setup(e => e.Instance).Returns(new object());
        eventData.Setup(e => e.Properties).Returns(properties);
        return eventData;
    }

    private static ScopedTracer CreateTracer()
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.DataStreamsMonitoring.Enabled, "true" },
        }));
        return TracerHelper.CreateWithFakeAgent(settings);
    }
}
