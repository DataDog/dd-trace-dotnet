// <copyright file="HttpHeaderHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Telemetry.Transports;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams;

public class HttpHeaderHelperTests
{
    [Fact]
    public async Task DataStreamsHttpHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(DataStreamsHttpHeaderHelper.Instance);

    [Fact]
    public async Task EventPlatformHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(EventPlatformHeaderHelper.Instance);

    [Fact]
    public async Task MinimalAgentHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(MinimalAgentHeaderHelper.Instance);

    [Fact]
    public async Task MinimalWithContainerIdAgentHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(new MinimalWithContainerIdAgentHeaderHelper("test-container-id-12345"));

    [Fact]
    public async Task TelemetryAgentHttpHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(TelemetryAgentHttpHeaderHelper.Instance);

    [Fact]
    public async Task TraceAgentHttpHeaderHelper_WritesExpectedHeaders() => await WriteLeadingHeaders(TraceAgentHttpHeaderHelper.Instance);

    private async Task WriteLeadingHeaders(HttpHeaderHelperBase helper)
    {
        // Note that WriteLeadingHeaders should NOT write this header in WriteLeadingHeaders
        var request = new HttpRequest(
            verb: "PATCH",
            host: "my-host.com",
            path: "/some/path",
            headers: new HttpHeaders { { "x-test", "my-value" } },
            content: new BufferContent(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{}"))));

        var leadingHeaders = string.Concat(helper.DefaultHeaders.Select(kvp => $"{kvp.Key}: {kvp.Value}{DatadogHttpValues.CrLf}"));
        var expected = "PATCH /some/path HTTP/1.1\r\nHost: my-host.com\r\nAccept-Encoding: identity\r\nContent-Length: 2\r\n" + leadingHeaders;

        var sb = new StringBuilder();
        using var textWriter = new StringWriter(sb);
        await helper.WriteLeadingHeaders(request, textWriter);

        var actual = sb.ToString();
        actual.Should().Be(expected, "because headers added as leading should match the values returned in " + nameof(HttpHeaderHelperBase.DefaultHeaders));
    }
}
