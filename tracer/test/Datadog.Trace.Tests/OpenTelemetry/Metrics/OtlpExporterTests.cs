// <copyright file="OtlpExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

using System;
using Datadog.Trace.OpenTelemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Metrics;

public class OtlpExporterTests
{
    [Fact]
    public void CreateHttpClient_TcpEndpoint_SetsTracingDisabledHeader()
    {
        var endpoint = new Uri("http://localhost:4318");

        using var client = OtlpExporter.CreateHttpClient(endpoint);

        client.DefaultRequestHeaders.TryGetValues(HttpHeaderNames.TracingEnabled, out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("false");
    }

    [Fact]
    public void CreateHttpClient_UnixEndpoint_SetsTracingDisabledHeader()
    {
        var endpoint = new Uri("unix:///var/run/datadog/apm.socket");

        using var client = OtlpExporter.CreateHttpClient(endpoint);

        client.DefaultRequestHeaders.TryGetValues(HttpHeaderNames.TracingEnabled, out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("false");
    }
}

#endif
