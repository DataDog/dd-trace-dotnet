// <copyright file="OtlpExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER

using System.Collections.Generic;
using Datadog.Trace.OpenTelemetry.Logs;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Logs;

public class OtlpExporterTests
{
    [Fact]
    public void CreateHttpClient_SetsTracingDisabledHeader()
    {
        using var client = OtlpExporter.CreateHttpClient(timeoutMs: 5000, headers: new Dictionary<string, string>());

        client.DefaultRequestHeaders.TryGetValues(HttpHeaderNames.TracingEnabled, out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("false");
    }

    [Fact]
    public void CreateHttpClient_IncludesCustomHeaders()
    {
        var customHeaders = new Dictionary<string, string> { ["X-My-Header"] = "my-value" };

        using var client = OtlpExporter.CreateHttpClient(timeoutMs: 5000, headers: customHeaders);

        client.DefaultRequestHeaders.TryGetValues("X-My-Header", out var values).Should().BeTrue();
        values.Should().ContainSingle().Which.Should().Be("my-value");
    }
}

#endif
