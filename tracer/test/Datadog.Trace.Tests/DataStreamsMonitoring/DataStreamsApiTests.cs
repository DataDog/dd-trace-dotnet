// <copyright file="DataStreamsApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsApiTests
{
    [Fact]
    public async Task SendAsync_When200_ReturnsTrue()
    {
        var factory = new TestRequestFactory(x => new TestApiRequest(x));
        var api = new DataStreamsApi(factory);

        var result = await api.SendAsync(new ArraySegment<byte>(new byte[64]));
        factory.RequestsSent.Should().HaveCount(1);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task SendAsync_WhenError_ReturnsFalse_AndDoesntRetry(int statusCode)
    {
        var factory = new TestRequestFactory(x => new TestApiRequest(x, statusCode));
        var api = new DataStreamsApi(factory);

        var result = await api.SendAsync(new ArraySegment<byte>(new byte[64]));
        factory.RequestsSent.Should().HaveCount(1);
        result.Should().BeFalse();
    }
}
