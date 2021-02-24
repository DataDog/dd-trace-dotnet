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
    }
}
