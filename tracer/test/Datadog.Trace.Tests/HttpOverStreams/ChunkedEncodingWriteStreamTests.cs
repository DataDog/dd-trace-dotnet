// <copyright file="ChunkedEncodingWriteStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams;

public class ChunkedEncodingWriteStreamTests
{
    [Theory]
    [InlineData(0, 1, "0")]
    [InlineData(15, 1, "F")]
    [InlineData(12345, 4, "3039")]
    [InlineData(int.MaxValue, 8, "7FFFFFFF")]
    public void WriteChunkedEncodingHeaderToBuffer_GivesCorrectOutput(int count, int bytes, string expected)
    {
        var buffer = new byte[8];
        var bytesWritten = ChunkedEncodingWriteStream.WriteChunkedEncodingHeaderToBuffer(buffer, count);
        var result = Encoding.UTF8.GetString(buffer, index: 0, count: bytesWritten);

        using var x = new AssertionScope();
        bytesWritten.Should().Be(bytes);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task HttpStreamIsWrittenCorrectly()
    {
        const int chunks = 5;
        const int bytesPerChunk = 30; // 1E in hex
        var data = Enumerable.Repeat((byte)43, bytesPerChunk).ToArray();

        using var ms = new MemoryStream();

        // need to make sure we always use CRLF, and get the line breaks etc all correct
        var headers =
            """
            POST / HTTP/1.1
            Host: localhost:8000
            Accept: text/html
            Content-Type: application/octet-stream
            Transfer-Encoding: chunked
            """.Trim();

        headers += $"{Environment.NewLine}{Environment.NewLine}";

        if (Environment.NewLine.Length == 1)
        {
            headers = headers.Replace(Environment.NewLine, "\r\n");
        }

        // this part isn't chunked
        var bytes = Encoding.ASCII.GetBytes(headers);
        await ms.WriteAsync(bytes, 0, bytes.Length);

        // this part should be
        using var chunked = new ChunkedEncodingWriteStream(ms);
        for (var i = 0; i < chunks; i++)
        {
            await chunked.WriteAsync(data, 0, data.Length);
        }

        await chunked.FinishAsync();

        // Now check what we got
        ms.Position = 0;
        var sr = new StreamReader(ms);
        var result = await sr.ReadToEndAsync();

        var expected =
            """
                POST / HTTP/1.1
                Host: localhost:8000
                Accept: text/html
                Content-Type: application/octet-stream
                Transfer-Encoding: chunked

                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                0
                """.Trim() + Environment.NewLine + Environment.NewLine;

        // fix the expected line breaks in response
        if (Environment.NewLine.Length == 1)
        {
            expected = expected.Replace(Environment.NewLine, "\r\n");
        }

        result.Should().Be(expected);
    }

    [Fact]
    public async Task ChunkedEncodingIsReadCorrectly()
    {
        var input =
            """
                POST /test HTTP/1.1
                Host: localhost:8000
                Accept: text/html
                Content-Type: application/octet-stream
                Transfer-Encoding: chunked

                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                1E
                ++++++++++++++++++++++++++++++
                0
                """.Trim() + Environment.NewLine + Environment.NewLine;

        // fix the expected line breaks in response
        if (Environment.NewLine.Length == 1)
        {
            input = input.Replace(Environment.NewLine, "\r\n");
        }

        using var ms = new MemoryStream();
        using var sw = new StreamWriter(ms, new UTF8Encoding(false), bufferSize: 100, leaveOpen: true);
        await sw.WriteAsync(input);
        await sw.FlushAsync();

        ms.Position = 0;
        var request = await MockHttpParser.ReadRequest(ms);

        request.Method.Should().Be("POST");
        request.PathAndQuery.Should().Be("/test");
        request.Headers
               .SelectMany(x => x.Value.Select(y => new KeyValuePair<string, string>(x.Key, y)))
               .Should()
               .Equal(new Dictionary<string, string>
                {
                    { "Host", "localhost:8000" },
                    { "Accept", "text/html" },
                    { "Content-Type", "application/octet-stream" },
                    { "Transfer-Encoding", "chunked" },
                });

        using var sr = new StreamReader(new MemoryStream(request.ReadStreamBody()));
        var output = await sr.ReadToEndAsync();
        output.Should().Be("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
    }
}
