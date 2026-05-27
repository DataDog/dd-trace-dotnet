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
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using HttpMultipartParser;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger.SymbolsTests;

[UsesVerify]
public class SymbolUploadApiTests
{
    public SymbolUploadApiTests()
    {
        VerifyHelper.InitializeGlobalSettings();
    }

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
        var metadata = new SymDbUploadMetadata(
            Service: "benchmark-service",
            Version: "1.0.0",
            UploadId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            BatchNum: 7,
            Final: false);
        var requestFactory = new CapturingRequestFactory();
        var discoveryService = new DiscoveryServiceMock();
        var api = SymbolUploadApi.Create(requestFactory, discoveryService, new NullGitMetadataProvider(), enableCompression);
        discoveryService.TriggerChange(symbolDbEndpoint: "symdb/v1/input");

        var result = await api
                          .SendBatchAsync(
                               static async (stream, state) =>
                               {
                                   var bytes = Encoding.UTF8.GetBytes(state);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               symbolsJson,
                               metadata);

        result.Should().BeTrue();
        await Verifier.Verify(ParseRequestForVerify(requestFactory.Request, enableCompression))
                      .UseFileName($"{nameof(SymbolUploadApiTests)}.SendBatchAsync_WritesExpectedMultipartWireFormat.{(enableCompression ? "compressed" : "uncompressed")}")
                      .DisableRequireUniquePrefix();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendBatchAsync_RetriesWithFreshRequestAndReplaysMultipartBody(bool enableCompression)
    {
        const string symbolsJson = """{"service":"benchmark","scopes":[{"name":"type"}]}""";
        var metadata = new SymDbUploadMetadata(
            Service: "benchmark-service",
            Version: "1.0.0",
            UploadId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
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
        var writerCalls = new MutableInt();

        var result = await api
                          .SendBatchAsync(
                               static async (stream, state) =>
                               {
                                   state.WriterCalls.Value++;
                                   var bytes = Encoding.UTF8.GetBytes(state.SymbolsJson);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               (SymbolsJson: symbolsJson, WriterCalls: writerCalls),
                               metadata);

        result.Should().BeTrue();
        writerCalls.Value.Should().Be(2);
        delays.Should().Equal(TimeSpan.FromSeconds(3));
        requestFactory.Requests.Should().HaveCount(2);
        requestFactory.Requests[0].Should().NotBeSameAs(requestFactory.Requests[1]);
        ParseRequestForVerify(requestFactory.Requests[1], enableCompression)
           .Should()
           .BeEquivalentTo(ParseRequestForVerify(requestFactory.Requests[0], enableCompression));
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
                               static async (stream, state) =>
                               {
                                   var bytes = Encoding.UTF8.GetBytes(state);
                                   await stream.WriteAsync(bytes, 0, bytes.Length);
                               },
                               symbolsJson,
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

    private static object ParseRequestForVerify(CapturingRequest request, bool enableCompression)
    {
        using var bodyStream = new MemoryStream(request.Body);
        var form = MultipartFormDataParser.Parse(bodyStream, DatadogHttpValues.Boundary, Encoding.UTF8);
        form.Parameters.Should().BeEmpty();
        form.Files.Should().HaveCount(2);

        var filePart = form.Files[0];
        var eventPart = form.Files[1];
        var fileContent = ReadBytes(filePart.Data);
        var eventContent = ReadBytes(eventPart.Data);
        var eventJson = JObject.Parse(Encoding.UTF8.GetString(eventContent));
        eventJson["runtimeId"] = "<runtime-id>";

        return new
        {
            ContentType = request.ContentType,
            Files = new[]
            {
                new
                {
                    filePart.Name,
                    filePart.FileName,
                    filePart.ContentType,
                    Length = fileContent.Length,
                    Content = Encoding.UTF8.GetString(enableCompression ? Decompress(fileContent) : fileContent)
                },
                new
                {
                    eventPart.Name,
                    eventPart.FileName,
                    eventPart.ContentType,
                    Length = eventContent.Length,
                    Content = eventJson.ToString(Formatting.Indented)
                }
            }
        };
    }

    private static byte[] ReadBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var compressedStream = new MemoryStream(compressed);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        gzipStream.CopyTo(decompressed);
        return decompressed.ToArray();
    }

    private sealed class MutableInt
    {
        public int Value { get; set; }
    }

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
            return new TestApiResponse(_statusCode, "{}", MimeTypes.Json);
        }

        public Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            throw new NotImplementedException();
        }
    }
}
