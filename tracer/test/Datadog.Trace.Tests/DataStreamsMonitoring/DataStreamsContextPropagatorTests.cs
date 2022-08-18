// <copyright file="DataStreamsContextPropagatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsContextPropagatorTests
{
    [Fact]
    public void CanRoundTripPathwayContext()
    {
        var headers = new TestHeadersCollection();
        var context = new PathwayContext(
            new PathwayHash(1234),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000, // leaky abstraction, the encoder truncates
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000);

        DataStreamsContextPropagator.Instance.Inject(context, headers);

        var extracted = DataStreamsContextPropagator.Instance.Extract(headers);

        extracted.Should().NotBeNull();
        extracted.Value.Hash.Value.Should().Be(context.Hash.Value);
        extracted.Value.PathwayStart.Should().Be(context.PathwayStart);
        extracted.Value.EdgeStart.Should().Be(context.EdgeStart);
    }
}
