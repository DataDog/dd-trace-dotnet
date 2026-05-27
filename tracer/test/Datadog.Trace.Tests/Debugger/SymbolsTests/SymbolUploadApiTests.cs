// <copyright file="SymbolUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

public class SymbolUploadApiTests
{
    [Fact]
    public void EventMetadata_IsValidJson_AndContainsAllFields()
    {
        // Include quotes to ensure proper JSON escaping/quoting
        const string serviceName = "test\"service";
        const string version = "1.0.0";
        const string runtimeId = "runtime-id";
        var uploadId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        const long batchNum = 7;
        const int attachmentSize = 12345;

        var metadata = new SymDbUploadMetadata(
            Service: serviceName,
            Version: version,
            UploadId: uploadId,
            BatchNum: batchNum,
            Final: false);
        var bytes = SymbolUploadApi.CreateEventMetadata(metadata, runtimeId, attachmentSize);
        var json = Encoding.UTF8.GetString(bytes.Array!, bytes.Offset, bytes.Count);

        var jobj = JObject.Parse(json);

        Assert.Equal("dd_debugger", (string?)jobj["ddsource"]);
        Assert.Equal(serviceName, (string?)jobj["service"]);
        Assert.Equal(version, (string?)jobj["version"]);
        Assert.Equal("dotnet", (string?)jobj["language"]);
        Assert.Equal(runtimeId, (string?)jobj["runtimeId"]);
        Assert.Equal("symdb", (string?)jobj["type"]);
        Assert.Equal(uploadId.ToString(), (string?)jobj["uploadId"]);
        Assert.Equal(batchNum, (long?)jobj["batchNum"]);
        Assert.Equal(false, (bool?)jobj["final"]);
        Assert.Equal(attachmentSize, (int?)jobj["attachmentSize"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendBatchAsync_WritesExpectedMultipartWireFormat(bool enableCompression)
    {
        const string symbolsJson = """{"service":"benchmark","scopes":[]}""";
        var uploadId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var metadata = new SymDbUploadMetadata(
            Service: "benchmark-service",
            Version: "1.0.0",
            UploadId: uploadId,
            BatchNum: 7,
            Final: false);
        var requestFactory = new CapturingRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        var api = SymbolUploadApi.Create(requestFactory, discoveryService, new NullGitMetadataProvider(), enableCompression);
        discoveryService.TriggerChange(symbolDbEndpoint: "symdb/v1/input");

        var result = await api
                          .SendBatchAsync(
                               async stream =>
                               {
                                   var bytes = Encoding.UTF8.GetBytes(symbolsJson);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               metadata);

        result.Should().BeTrue();
        AssertMultipartRequest(requestFactory.Request, metadata, uploadId, symbolsJson, enableCompression);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendBatchAsync_RetriesWithFreshRequestAndReplaysMultipartBody(bool enableCompression)
    {
        const string symbolsJson = """{"service":"benchmark","scopes":[{"name":"type"}]}""";
        var uploadId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var metadata = new SymDbUploadMetadata(
            Service: "benchmark-service",
            Version: "1.0.0",
            UploadId: uploadId,
            BatchNum: 7,
            Final: false);
        var requestFactory = new CapturingRequestFactory(500, 200);
        var discoveryService = new DiscoveryServiceMock();
        var delays = new List<TimeSpan>();
        var api = SymbolUploadApi.Create(
            requestFactory,
            discoveryService,
            new NullGitMetadataProvider(),
            enableCompression,
            delay =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        discoveryService.TriggerChange(symbolDbEndpoint: "symdb/v1/input");
        var writerCalls = 0;

        var result = await api
                          .SendBatchAsync(
                               async stream =>
                               {
                                   writerCalls++;
                                   var bytes = Encoding.UTF8.GetBytes(symbolsJson);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               metadata);

        result.Should().BeTrue();
        writerCalls.Should().Be(2);
        delays.Should().Equal(TimeSpan.FromSeconds(3));
        requestFactory.Requests.Should().HaveCount(2);
        requestFactory.Requests[0].Should().NotBeSameAs(requestFactory.Requests[1]);
        AssertMultipartRequest(requestFactory.Requests[0], metadata, uploadId, symbolsJson, enableCompression);
        AssertMultipartRequest(requestFactory.Requests[1], metadata, uploadId, symbolsJson, enableCompression);
    }

    [Fact]
    public async Task SendBatchAsync_DoesNotDelayAfterFinalRetry()
    {
        const string symbolsJson = """{"service":"benchmark","scopes":[{"name":"type"}]}""";
        var metadata = new SymDbUploadMetadata(
            Service: "benchmark-service",
            Version: "1.0.0",
            UploadId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            BatchNum: 7,
            Final: false);
        var requestFactory = new CapturingRequestFactory(500, 500, 500);
        var discoveryService = new DiscoveryServiceMock();
        var delays = new List<TimeSpan>();
        var api = SymbolUploadApi.Create(
            requestFactory,
            discoveryService,
            new NullGitMetadataProvider(),
            enableCompression: false,
            delayAsync: delay =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        discoveryService.TriggerChange(symbolDbEndpoint: "symdb/v1/input");

        var result = await api
                          .SendBatchAsync(
                               async stream =>
                               {
                                   var bytes = Encoding.UTF8.GetBytes(symbolsJson);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               metadata);

        result.Should().BeFalse();
        requestFactory.Requests.Should().HaveCount(3);
        delays.Should().Equal(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6));
    }

    [Fact]
    public void DiscoverySubscription_IsRemovedAfterSymbolDbEndpointIsDiscovered()
    {
        var requestFactory = new CapturingRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        _ = SymbolUploadApi.Create(requestFactory, discoveryService, new NullGitMetadataProvider(), enableCompression: false);

        discoveryService.Callbacks.Should().HaveCount(1);

        discoveryService.TriggerChange(symbolDbEndpoint: null);
        discoveryService.Callbacks.Should().HaveCount(1);

        discoveryService.TriggerChange(symbolDbEndpoint: "symdb/v1/input");

        discoveryService.Callbacks.Should().BeEmpty();
    }

    private static void AssertMultipartRequest(CapturingRequest request, SymDbUploadMetadata metadata, Guid uploadId, string symbolsJson, bool enableCompression)
    {
        request.ContentType.Should().Be("multipart/form-data; boundary=" + DatadogHttpValues.Boundary);
        request.MultipartBoundary.Should().Be(DatadogHttpValues.Boundary);

        var parts = ParseMultipart(request.Body);
        parts.Should().HaveCount(2);

        var filePart = parts[0];
        filePart.Headers.Should().Contain("Content-Disposition: form-data; name=\"file\"; filename=\"" + (enableCompression ? "file.gz" : "file.json") + "\"");
        filePart.Headers.Should().Contain("Content-Type: " + (enableCompression ? MimeTypes.Gzip : MimeTypes.Json));

        var fileBytes = enableCompression ? Decompress(filePart.Content) : filePart.Content;
        Encoding.UTF8.GetString(fileBytes).Should().Be(symbolsJson);

        var eventPart = parts[1];
        eventPart.Headers.Should().Contain("Content-Disposition: form-data; name=\"event\"; filename=\"event.json\"");
        eventPart.Headers.Should().Contain("Content-Type: " + MimeTypes.Json);
        var eventJson = JObject.Parse(Encoding.UTF8.GetString(eventPart.Content));
        eventJson["ddsource"]!.Value<string>().Should().Be("dd_debugger");
        eventJson["service"]!.Value<string>().Should().Be(metadata.Service);
        eventJson["version"]!.Value<string>().Should().Be(metadata.Version);
        eventJson["language"]!.Value<string>().Should().Be("dotnet");
        eventJson["runtimeId"]!.Value<string>().Should().NotBeNullOrEmpty();
        eventJson["type"]!.Value<string>().Should().Be("symdb");
        eventJson["uploadId"]!.Value<string>().Should().Be(uploadId.ToString());
        eventJson["batchNum"]!.Value<long>().Should().Be(metadata.BatchNum);
        eventJson["final"]!.Value<bool>().Should().BeFalse();
        eventJson["attachmentSize"]!.Value<int>().Should().Be(filePart.Content.Length);
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzipStream.CopyTo(decompressed);
        return decompressed.ToArray();
    }

    private static MultipartPart[] ParseMultipart(byte[] body)
    {
        var boundary = Encoding.UTF8.GetBytes("--" + DatadogHttpValues.Boundary);
        var crlf = Encoding.UTF8.GetBytes("\r\n");
        var headerSeparator = Encoding.UTF8.GetBytes("\r\n\r\n");
        var parts = new System.Collections.Generic.List<MultipartPart>();
        var position = 0;

        while (position < body.Length)
        {
            var boundaryIndex = IndexOf(body, boundary, position);
            if (boundaryIndex < 0)
            {
                break;
            }

            position = boundaryIndex + boundary.Length;
            if (position + 2 <= body.Length && body[position] == '-' && body[position + 1] == '-')
            {
                break;
            }

            if (!StartsWith(body, crlf, position))
            {
                throw new InvalidOperationException("Expected CRLF after multipart boundary.");
            }

            position += crlf.Length;
            var headerEnd = IndexOf(body, headerSeparator, position);
            if (headerEnd < 0)
            {
                throw new InvalidOperationException("Expected multipart header separator.");
            }

            var headers = Encoding.UTF8.GetString(body, position, headerEnd - position)
                                  .Split(new[] { "\r\n" }, StringSplitOptions.None);
            position = headerEnd + headerSeparator.Length;

            var nextBoundary = IndexOf(body, boundary, position);
            if (nextBoundary < 0)
            {
                throw new InvalidOperationException("Expected next multipart boundary.");
            }

            var contentLength = nextBoundary - position;
            if (contentLength >= crlf.Length &&
                body[nextBoundary - 2] == '\r' &&
                body[nextBoundary - 1] == '\n')
            {
                contentLength -= crlf.Length;
            }

            var content = new byte[contentLength];
            Array.Copy(body, position, content, 0, content.Length);
            parts.Add(new MultipartPart(headers, content));
            position = nextBoundary;
        }

        return parts.ToArray();
    }

    private static bool StartsWith(byte[] source, byte[] value, int startIndex)
    {
        if (startIndex + value.Length > source.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (source[startIndex + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf(byte[] source, byte[] value, int startIndex)
    {
        for (var i = startIndex; i <= source.Length - value.Length; i++)
        {
            var found = true;
            for (var j = 0; j < value.Length; j++)
            {
                if (source[i + j] != value[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct MultipartPart(string[] Headers, byte[] Content);

    private sealed class CapturingRequestFactory : IApiRequestFactory
    {
        private readonly Uri _baseEndpoint = new("http://localhost:8126/");
        private readonly Queue<int> _statusCodes;

        public CapturingRequestFactory(params int[] statusCodes)
        {
            _statusCodes = new Queue<int>(statusCodes.Length == 0 ? new[] { 200 } : statusCodes);
        }

        public List<CapturingRequest> Requests { get; } = new();

        public CapturingRequest Request => Requests[0];

        public string Info(Uri endpoint)
            => endpoint.ToString();

        public Uri GetEndpoint(string relativePath)
            => UriHelpers.Combine(_baseEndpoint, relativePath);

        public IApiRequest Create(Uri endpoint)
        {
            var statusCode = _statusCodes.Count > 0 ? _statusCodes.Dequeue() : 200;
            var request = new CapturingRequest(statusCode);
            Requests.Add(request);
            return request;
        }

        public void SetProxy(WebProxy proxy, NetworkCredential credential)
        {
        }
    }

    private sealed class CapturingRequest : IApiRequest
    {
        private readonly int _statusCode;

        public CapturingRequest(int statusCode)
        {
            _statusCode = statusCode;
        }

        public byte[] Body { get; private set; } = [];

        public string? ContentType { get; private set; }

        public string? MultipartBoundary { get; private set; }

        public void AddHeader(string name, string value)
        {
        }

        public Task<IApiResponse> GetAsync()
            => Task.FromResult<IApiResponse>(new TestApiResponse(_statusCode, "{}", MimeTypes.Json));

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, contentEncoding: null);

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string? contentEncoding)
        {
            Body = new byte[bytes.Count];
            Array.Copy(bytes.Array!, bytes.Offset, Body, 0, bytes.Count);
            ContentType = contentType;
            return Task.FromResult<IApiResponse>(new TestApiResponse(_statusCode, "{}", MimeTypes.Json));
        }

        public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression)
            => PostAsJsonAsync(payload, compression, SerializationHelpers.DefaultJsonSettings);

        public Task<IApiResponse> PostAsJsonAsync<T>(T payload, MultipartCompression compression, JsonSerializerSettings settings)
        {
            ContentType = MimeTypes.Json;
            return Task.FromResult<IApiResponse>(new TestApiResponse(_statusCode, "{}", MimeTypes.Json));
        }

        public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
        {
            using var stream = new MemoryStream();
            await writeToRequestStream(stream).ConfigureAwait(false);
            Body = stream.ToArray();
            ContentType = ContentTypeHelper.GetContentType(contentType, multipartBoundary);
            MultipartBoundary = multipartBoundary;
            return new TestApiResponse(_statusCode, "{}", MimeTypes.Json);
        }

        public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            throw new NotImplementedException();
        }
    }
}
