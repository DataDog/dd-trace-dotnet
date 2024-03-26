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

    private static DateTimeOffset FromUnixTimeNanoseconds(long nanoseconds)
        => DateTimeOffset.FromUnixTimeMilliseconds(nanoseconds / 1_000_000);
}
