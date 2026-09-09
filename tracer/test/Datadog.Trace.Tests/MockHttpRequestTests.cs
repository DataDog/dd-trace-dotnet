// <copyright file="MockHttpRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests;

[Collection(nameof(WebRequestCollection))]
public class MockHttpRequestTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void GzipDiagnosticsPreserveTheBodyAndFailure(bool validGzip, bool contentLengthKnown)
    {
        byte[] message = [1, 2, 3, 4];
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(message, 0, message.Length);
        }

        var body = validGzip ? compressed.ToArray() : message;
        using var stream = new MemoryStream(body);
        var headers = new MockHttpRequest.MockHeaders();
        headers.Add("Content-Encoding", "gzip");
        headers.Add("Authorization", "must-not-be-captured");
        headers.Add(GzipDiagnosticCapture.RequestHeader, "request-123");
        var request = new MockHttpRequest { Body = stream, Headers = headers, ContentLength = contentLengthKnown ? body.Length : null, PathAndQuery = "/evp_proxy/v4/api/v2/citestcycle" };
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            if (validGzip)
            {
                request.ReadStreamBody(directory).Should().Equal(message);
                Directory.Exists(directory).Should().BeFalse();
            }
            else
            {
                Action read = () => request.ReadStreamBody(directory);
                read.Should().Throw<InvalidDataException>();
                File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory, "*.bin"))).Should().Equal(body);
                var metadata = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*.txt")));
                metadata.Should().Contain("Content-Encoding: gzip").And.Contain("Received bytes: 4").And.Contain("Request: request-123").And.NotContain("must-not-be-captured");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(200, false)]
    [InlineData(500, false)]
    [InlineData(500, true)]
    [InlineData(0, false)]
    public async Task SenderPreservesResponseAndOriginalBytes(int statusCode, bool mutateBuffer)
    {
        byte[] buffer = [99, 1, 2, 3, 4, 99];
        var body = new ArraySegment<byte>(buffer, 1, 4);
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var response = new Mock<IApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(statusCode);
        var request = new Mock<IApiRequest>();
        string requestId = null;
        request.Setup(r => r.AddHeader(GzipDiagnosticCapture.RequestHeader, It.IsAny<string>()))
               .Callback<string, string>((_, value) => requestId = value);
        request.Setup(r => r.PostAsync(body, "application/msgpack", "gzip"))
               .Returns(() =>
                {
                    if (mutateBuffer)
                    {
                        buffer[1] = 42;
                    }

                    return statusCode == 0 ? Task.FromException<IApiResponse>(new IOException("transport failure")) : Task.FromResult(response.Object);
                });
        try
        {
            if (statusCode == 0)
            {
                Func<Task> send = () => GzipDiagnosticCapture.SendAsync(request.Object, body, directory);
                await send.Should().ThrowAsync<IOException>().WithMessage("transport failure");
            }
            else
            {
                (await GzipDiagnosticCapture.SendAsync(request.Object, body, directory)).Should().BeSameAs(response.Object);
            }

            if (statusCode == 200)
            {
                Directory.Exists(directory).Should().BeFalse();
            }
            else
            {
                File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory, "*.bin"))).Should().Equal(1, 2, 3, 4);
                var metadata = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*.txt")));
                metadata.Should().Contain($"Request: {requestId}").And.Contain("Original bytes: 4").And.Contain($"HTTP status: {(statusCode == 0 ? -1 : statusCode)}").And.Contain("Process: ");
                var before = metadata.Split(new[] { "SHA256 before send: " }, StringSplitOptions.None)[1].Split('\n')[0];
                var after = metadata.Split(new[] { "SHA256 after send: " }, StringSplitOptions.None)[1].Split('\n')[0];
                (before == after).Should().Be(!mutateBuffer);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RejectedBodyCanBeMatchedAtBothEnds()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        byte[] invalidGzip = [1, 2, 3, 4];
        using var agent = MockTracerAgent.Create(output);
        agent.DecompressionFailureDirectory = directory;
        var url = new Uri($"http://127.0.0.1:{agent.Port}/evp_proxy/v4/api/v2/citestcycle");
        var sender = new ApiWebRequestFactory(url, AgentHttpHeaderNames.DefaultHeaders).Create(url);
        try
        {
            using var response = await GzipDiagnosticCapture.SendAsync(sender, new ArraySegment<byte>(invalidGzip), directory);
            response.StatusCode.Should().Be(500);
            Directory.GetFiles(directory, "*.bin").Should().HaveCount(2);
            var receiverMetadata = File.ReadAllText(Path.Combine(directory, "capture-0.txt"));
            receiverMetadata.Should().Contain("Side: receiver");
            var requestId = receiverMetadata.Split(new[] { "Request: " }, StringSplitOptions.None)[1].Split('\n')[0];
            foreach (var file in Directory.GetFiles(directory, "*.bin"))
            {
                File.ReadAllBytes(file).Should().Equal(invalidGzip);
                File.ReadAllText(Path.ChangeExtension(file, ".txt")).Should().Contain($"Request: {requestId}");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CaptureLimitIsSharedByConcurrentWritersAndMarksTruncation()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            GzipDiagnosticCapture.Save(directory, new ArraySegment<byte>(new byte[GzipDiagnosticCapture.MaxBodyBytes + 1]), "large body");
            new FileInfo(Path.Combine(directory, "capture-0.bin")).Length.Should().Be(GzipDiagnosticCapture.MaxBodyBytes);
            File.ReadAllText(Path.Combine(directory, "capture-0.txt")).Should().Contain("Truncated: True");
            Parallel.For(0, GzipDiagnosticCapture.MaxCaptures + 8, _ => GzipDiagnosticCapture.Save(directory, new ArraySegment<byte>(new byte[] { 1 }), "parallel"));
            Directory.GetFiles(directory, "*.bin").Should().HaveCount(GzipDiagnosticCapture.MaxCaptures);
            Directory.GetFiles(directory, "*.txt").Should().HaveCount(GzipDiagnosticCapture.MaxCaptures);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CaptureFailureDoesNotReplaceDecompressionFailure()
    {
        var directory = Path.GetTempFileName();
        try
        {
            using var body = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var headers = new MockHttpRequest.MockHeaders();
            headers.Add("Content-Encoding", "gzip");
            var request = new MockHttpRequest { Body = body, Headers = headers };
            Action read = () => request.ReadStreamBody(directory);
            read.Should().Throw<InvalidDataException>();
        }
        finally
        {
            File.Delete(directory);
        }
    }
}
