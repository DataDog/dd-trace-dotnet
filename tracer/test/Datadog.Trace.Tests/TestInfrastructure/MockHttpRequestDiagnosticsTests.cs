// <copyright file="MockHttpRequestDiagnosticsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests;

public class MockHttpRequestDiagnosticsTests(ITestOutputHelper output)
{
    // Python gzip.compress(b'{"events":[]}', mtime=0). No .NET compressor is needed to create the fixture.
    private const string GzipBody = "H4sIAAAAAAACCqtWSi1LzSspVrKKjq0FADp5TF8NAAAA";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturesRawBodyAndHttpOutcome(bool corruptChecksum)
    {
        var body = Convert.FromBase64String(GzipBody);
        if (corruptChecksum)
        {
            body[body.Length - 8] ^= 1;
        }

        using var agent = MockTracerAgent.Create(output);
        MockHttpRequest received = null;
        agent.RequestReceived += (_, e) =>
        {
            e.Value.Diagnostics = new MockHttpRequest.RequestDiagnostics();
            received = e.Value;
        };

        using var client = new HttpClient();
        using var content = new ByteArrayContent(body);
        content.Headers.Add("Content-Type", "application/json");
        content.Headers.Add("Content-Encoding", "gzip");
        using var response = await client.PostAsync($"http://localhost:{agent.Port}/evp_proxy/v4/api/v2/citestcycle", content);

        var expectedStatus = corruptChecksum ? HttpStatusCode.InternalServerError : HttpStatusCode.OK;
        response.StatusCode.Should().Be(expectedStatus);
        received.Diagnostics.ResponseStatusCode.Should().Be((int)expectedStatus);
        received.Diagnostics.Body.ToArray().Should().Equal(body);
        if (corruptChecksum)
        {
            received.Diagnostics.Exception.Should().BeOfType<InvalidDataException>();
            received.Diagnostics.DecodedBodyInJson.Should().BeNull();
        }
        else
        {
            received.Diagnostics.Exception.Should().BeNull();
            received.Diagnostics.DecodedBodyInJson.Should().Be("{\"events\":[]}");
        }
    }

    [Fact]
    public void CapturesBodyWithoutContentLengthWhenDecodingFails()
    {
        var body = Convert.FromBase64String(GzipBody);
        body[body.Length - 8] ^= 1;
        using var stream = new MemoryStream(body);
        var headers = new MockHttpRequest.MockHeaders();
        headers.Add("Content-Encoding", "gzip");
        var request = new MockHttpRequest
        {
            Headers = headers,
            Body = stream,
            Diagnostics = new MockHttpRequest.RequestDiagnostics(),
        };

        var read = () => request.ReadStreamBody();
        read.Should().Throw<InvalidDataException>();
        request.Diagnostics.Body.ToArray().Should().Equal(body);
    }

    [Fact]
    public void CapturesOnlyReceivedBytesWhenBodyIsTruncated()
    {
        byte[] body = [1, 2, 3];
        using var stream = new MemoryStream(body);
        var request = new MockHttpRequest
        {
            Headers = new MockHttpRequest.MockHeaders(),
            Body = stream,
            ContentLength = body.Length + 1,
            Diagnostics = new MockHttpRequest.RequestDiagnostics(),
        };

        var read = () => request.ReadStreamBody();
        read.Should().Throw<Exception>().WithMessage("Less bytes were sent than we counted. 3 read versus 4 expected.");
        request.Diagnostics.Body.ToArray().Should().Equal(body);
    }
}
