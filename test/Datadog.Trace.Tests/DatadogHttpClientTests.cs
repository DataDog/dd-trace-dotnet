using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.HttpOverStreams.HttpContent;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class DatadogHttpClientTests
    {
        [Fact]
        public async Task DatadogHttpClient_CanParseResponse()
        {
            var client = new DatadogHttpClient();
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
            var client = new DatadogHttpClient();
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
                var bytesToRead = Math.Min(_bytesToRead, count);

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
