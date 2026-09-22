// <copyright file="MockHttpParserTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class MockHttpParserTests
    {
        [Fact]
        public async Task ReadRequest_ParsesRequestLineAndHeaders_AndLeavesBodyUnread()
        {
            const string body = "profile-payload";
            var raw = "POST /profiling/v1/input HTTP/1.1\r\n" +
                      "Host: localhost\r\n" +
                      $"Content-Length: {body.Length}\r\n" +
                      "\r\n" +
                      body;

            using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

            var request = await MockHttpParser.ReadRequest(stream);

            request.PathAndQuery.Should().Be("/profiling/v1/input");
            request.ContentLength.Should().Be(body.Length);

            // The parser stops right after the blank line that terminates the headers; the body
            // must remain in the stream so the caller can drain it explicitly (otherwise the next
            // read would parse the payload as a bogus request).
            var bytesRemaining = raw.Length - stream.Position;
            bytesRemaining.Should().Be(body.Length);
        }

        [Fact]
        public async Task ReadRequest_OnEmptyStream_ThrowsInvalidOperationException()
        {
            // A client that closed its end of the pipe before sending anything yields an immediate
            // end-of-stream, which the named-pipe handler treats as a normal disconnect.
            using var stream = new MemoryStream(Array.Empty<byte>());

            Func<Task> act = async () => await MockHttpParser.ReadRequest(stream);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ReadRequest_OnTruncatedRequest_ThrowsInvalidOperationException()
        {
            // The request line and a header start, but the stream ends before the headers are
            // terminated by the blank line.
            var raw = "POST /profiling/v1/input HTTP/1.1\r\nContent-Length: 10\r\n";
            using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

            Func<Task> act = async () => await MockHttpParser.ReadRequest(stream);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
