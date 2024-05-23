// <copyright file="DatadogHttpClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.HttpOverStreams
{
    public class DatadogHttpClientTests
    {
        [Fact]
        public async Task DatadogHttpClient_CanParseResponse()
        {
            var client = new DatadogHttpClient(new TraceAgentHttpHeaderHelper());
            var requestContent = new BufferContent(new ArraySegment<byte>(new byte[0]));
            var htmlResponse = string.Join("\r\n", HtmlResponseLines());
            using var requestStream = new MemoryStream();
            using var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlResponse));

            var request = new HttpRequest("POST", "localhost", string.Empty, new HttpHeaders(), requestContent);
            var response = await client.SendAsync(request, requestStream, responseStream);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("OK", response.ResponseMessage);
            Assert.Equal("Test Server", response.Headers.GetValue(("Server")));
            Assert.Equal(2, response.ContentLength);
            Assert.Equal("application/json", response.ContentType);

            var buffer = new byte[2];
            await response.Content.CopyToAsync(buffer);
            var content = Encoding.UTF8.GetString(buffer);
            Assert.Equal("{}", content);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        public async Task DatadogHttpClient_WhenOnlyPartOfResponseIsAvailable_ParsesCorrectly(int bytesToRead)
        {
            var client = new DatadogHttpClient(new TraceAgentHttpHeaderHelper());
            var requestContent = new BufferContent(new ArraySegment<byte>(new byte[0]));
            var htmlResponse = string.Join("\r\n", HtmlResponseLines());
            using var requestStream = new MemoryStream();
            var responseBytes = Encoding.UTF8.GetBytes(htmlResponse);
            using var responseStream = new RegulatedStream(responseBytes, bytesToRead);

            var request = new HttpRequest("POST", "localhost", string.Empty, new HttpHeaders(), requestContent);
            var response = await client.SendAsync(request, requestStream, responseStream);

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("OK", response.ResponseMessage);
            Assert.Equal("Test Server", response.Headers.GetValue(("Server")));
            Assert.Equal(2, response.ContentLength);
            Assert.Equal("application/json", response.ContentType);

            var buffer = new byte[2];
            await response.Content.CopyToAsync(buffer);
            var content = Encoding.UTF8.GetString(buffer);
            Assert.Equal("{}", content);
        }

        [Fact]
        public async Task DatadogHttpClient_CanHandleChunkedResponses()
        {
            // these are arbitrary - we test a range of values in ChunkedEncodingReadStreamTests, so not
            // much point in being exhaustive here
            const int chunks = 10;
            const int chunkSize = 257;
            const int bufferSize = 256;

            var chunk = ChunkedEncodingReadStreamTests.GetChunk(chunkSize);

            var sb = new StringBuilder();
            sb.Append("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nServer: Test Server\r\nTransfer-Encoding: chunked\r\n\r\n");

            for (var i = 0; i < chunks; i++)
            {
                sb.Append(chunkSize.ToString("X"))
                  .Append("\r\n")
                  .Append(chunk)
                  .Append("\r\n");
            }

            // final chunk
            sb.Append("0\r\n\r\n");

            var httpResponse = sb.ToString();
            // expected is the "un-chunked" response
            var expected = string.Join(string.Empty, Enumerable.Repeat(chunk, chunks));

            var client = new DatadogHttpClient(new TraceAgentHttpHeaderHelper());
            var requestContent = new BufferContent(new ArraySegment<byte>(new byte[0]));
            using var requestStream = new MemoryStream();
            var responseBytes = EncodingHelpers.Utf8NoBom.GetBytes(httpResponse);
            using var responseStream = new RegulatedStream(responseBytes, bufferSize);

            var request = new HttpRequest("POST", "localhost", string.Empty, new HttpHeaders(), requestContent);
            var response = await client.SendAsync(request, requestStream, responseStream);

            response.StatusCode.Should().Be(200);
            response.ResponseMessage.Should().Be("OK");
            response.Headers.GetValue("Server").Should().Be("Test Server");
            response.ContentType.Should().Be("application/json");
            response.ContentLength.Should().BeNull();
            response.Content.Length.Should().BeNull();

            using var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms);

            var actual = EncodingHelpers.Utf8NoBom.GetString(ms.GetBuffer(), index: 0, count: (int)ms.Length);
            actual.Should().Be(expected);
        }

        private static string[] HtmlResponseLines() =>
        new[]
        {
            "HTTP/1.1 200 OK",
            "Date: Mon, 27 Jul 2009 12:28:53 GMT",
            "Server: Test Server",
            "Last-Modified: Wed, 22 Jul 2009 19:15:56 GMT",
            "Content-Length: 2",
            "Content-Type: application/json",
            string.Empty,
            "{}"
        };

        public class RegulatedStream : Stream
        {
            private readonly byte[] _buffer;
            private readonly int _bytesToRead;

            public RegulatedStream(byte[] buffer, int bytesToRead)
            {
                _buffer = buffer;
                _bytesToRead = bytesToRead;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _buffer.Length;

            public override long Position { get; set; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var bytesRemaining = _buffer.Length - Position;
                var bytesToRead = (int)Math.Min(bytesRemaining, Math.Min(_bytesToRead, count));

                Buffer.BlockCopy(
                    src: _buffer,
                    srcOffset: (int)Position,
                    dst: buffer,
                    dstOffset: offset,
                    count: bytesToRead);

                Position += bytesToRead;
                return bytesToRead;
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }
    }
}
