// <copyright file="DataStreamsContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.ExtensionMethods;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsContextPropagatorTests
{
    [Fact]
    public void CanRoundTripPathwayContext()
    {
        var oneMs = TimeSpan.FromMilliseconds(1);
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(
            new PathwayHash(1234),
            DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
            DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

        DataStreamsContextPropagator.Instance.Inject(context, headers);

        var extracted = DataStreamsContextPropagator.Instance.Extract(headers);

        extracted.Should().NotBeNull();
        extracted.Value.Hash.Value.Should().Be(context.Hash.Value);
        FromUnixTimeNanoseconds(extracted.Value.PathwayStart)
           .Should()
           .BeCloseTo(FromUnixTimeNanoseconds(context.PathwayStart), oneMs);
        FromUnixTimeNanoseconds(extracted.Value.EdgeStart)
           .Should()
           .BeCloseTo(FromUnixTimeNanoseconds(context.EdgeStart), oneMs);
    }

    [Fact]
    public void Inject_WhenLegacyHeadersEnabled_IncludesBothBase64AndBinaryHeaders()
    {
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(
            new PathwayHash(1234),
            DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
            DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

        DataStreamsContextPropagator.Instance.Inject(context, headers);

        headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKeyBase64);
        headers.Values[DataStreamsPropagationHeaders.PropagationKeyBase64].Should().NotBeNullOrEmpty();

        headers.Values.Should().ContainKey(DataStreamsPropagationHeaders.PropagationKey);
        headers.Values[DataStreamsPropagationHeaders.PropagationKey].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Extract_WhenBothHeadersPresent_PrefersBase64Header()
    {
        var headers = new TestHeadersCollection();
        var originalContext = new PathwayContext(
            new PathwayHash(1234),
            DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeNanoseconds(),
            DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

        DataStreamsContextPropagator.Instance.Inject(originalContext, headers);

        var extractedContext = DataStreamsContextPropagator.Instance.Extract(headers);

        extractedContext.Should().NotBeNull();
        extractedContext.Value.Hash.Value.Should().Be(originalContext.Hash.Value);
        (extractedContext.Value.PathwayStart / 1_000_000).Should().Be(originalContext.PathwayStart / 1_000_000);
        (extractedContext.Value.EdgeStart / 1_000_000).Should().Be(originalContext.EdgeStart / 1_000_000);
    }

    private static DateTimeOffset FromUnixTimeNanoseconds(long nanoseconds)
        => DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000);
}
