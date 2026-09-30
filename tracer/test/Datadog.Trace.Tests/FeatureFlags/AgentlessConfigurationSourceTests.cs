// <copyright file="AgentlessConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class AgentlessConfigurationSourceTests
{
    private const string Body = """
        { "data": { "type": "universal-flag-configuration",
                    "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                    "environment": { "name": "production" }, "flags": {} } } }
        """;

    private const string EndpointUrl = "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server";

    [Fact]
    public async Task AppliesConfigurationFromA200()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().ContainSingle();
        applied[0].Environment!.Name.Should().Be("production");
        factory.RequestsSent.Should().ContainSingle();
    }

    [Fact]
    public async Task SendsTheEtagOfTheLastAppliedConfiguration()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();
        applied.Should().ContainSingle();

        // Second poll should send If-None-Match
        await source.PollAsync();
        factory.RequestsSent.Should().HaveCount(2);
        factory.RequestsSent[1].ExtraHeaders.Should().ContainKey("If-None-Match");
        factory.RequestsSent[1].ExtraHeaders["If-None-Match"].Should().Be("\"ufc-v1\"");
    }

    [Fact]
    public async Task RequestsTheConfiguredEnvironment()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, applied, environment: "production");

        await source.PollAsync();

        factory.RequestsSent[0].Endpoint.Should().Be(new Uri(EndpointUrl + "?dd_env=production"));
    }

    [Fact]
    public async Task DropsTheEtagWhenTheEnvironmentChanges()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }));
        using var source = CreateSource(factory, applied, environment: "production");

        await source.PollAsync();

        // The ETag identifies production's configuration, so it must not be sent against staging:
        // a 304 would pin the process to production's flags with no way back.
        source.UpdateEnvironment("staging");
        await source.PollAsync();

        factory.RequestsSent[1].Endpoint.Should().Be(new Uri(EndpointUrl + "?dd_env=staging"));
        factory.RequestsSent[1].ExtraHeaders.Should().NotContainKey("If-None-Match");
    }

    [Fact]
    public async Task DoesNotApplyOn304()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 304, responseContent: "{}"));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotApplyOn401()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 401, responseContent: "Unauthorized"));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotApplyOnMalformedPayload()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 200, responseContent: "not json"));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotApplyAfterDisposal()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        var source = CreateSource(factory, applied);

        // A shutdown mid-poll leaves the response unusable for a state transition.
        source.Dispose();
        await source.PollAsync();

        factory.RequestsSent.Should().ContainSingle();
        applied.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotApplyWhenDisposedAfterRequestSucceeds()
    {
        var applied = new List<ServerConfiguration>();
        AgentlessConfigurationSource? sourceRef = null;
        var factory = new TestRequestFactory(uri =>
        {
            var request = new DisposingApiRequest(uri, Body);
            request.Source = sourceRef;
            return request;
        });
        using var source = CreateSource(factory, applied);
        sourceRef = source;

        await source.PollAsync();

        applied.Should().BeEmpty();
    }

    [Fact]
    public async Task RetriesOn500ThenAppliesOnSuccess()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().ContainSingle();
        factory.RequestsSent.Should().HaveCount(2);
    }

    [Fact]
    public async Task RetriesUpToMaxAttemptsOn500()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
        factory.RequestsSent.Should().HaveCount(3);
    }

    [Fact]
    public async Task DoesNotRetryOn400()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 400, responseContent: "bad request"));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
        factory.RequestsSent.Should().ContainSingle();
    }

    [Fact]
    public async Task HandlesGzipResponse()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(uri => new GzipApiRequest(uri, Body));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().ContainSingle();
        applied[0].Environment!.Name.Should().Be("production");
    }

    [Fact]
    public async Task HandlesNetworkError()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new ThrowingApiRequest(uri),
            uri => new ThrowingApiRequest(uri),
            uri => new ThrowingApiRequest(uri));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().BeEmpty();
        factory.RequestsSent.Should().HaveCount(3);
    }

    private static AgentlessConfigurationSource CreateSource(
        TestRequestFactory factory,
        List<ServerConfiguration> applied,
        string? environment = null)
        => new(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            configuration =>
            {
                applied.Add(configuration);
                return true;
            },
            environment,
            waitAsync: NoWait);

    private static AgentlessEndpoint CreateEndpoint()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", baseUrl: null, out var endpoint, out _).Should().BeTrue();
        return endpoint ?? throw new InvalidOperationException("TryCreate reported success without producing an endpoint.");
    }

    private static Task NoWait(TimeSpan delay) => Task.CompletedTask;

    private class ThrowingApiRequest(Uri endpoint) : TestApiRequest(endpoint)
    {
        public override Task<IApiResponse> GetAsync() => throw new IOException("The connection was refused");
    }

    private class GzipApiRequest(Uri endpoint, string body) : TestApiRequest(endpoint)
    {
        public override Task<IApiResponse> GetAsync() => Task.FromResult<IApiResponse>(new GzipApiResponse(body));
    }

    private class GzipApiResponse(string body) : IApiResponse
    {
        public int StatusCode => 200;

        public long ContentLength => -1;

        public string? ContentTypeHeader => "application/json";

        public string? ContentEncodingHeader => "gzip";

        public void Dispose()
        {
        }

        public string? GetHeader(string headerName) => null;

        public Encoding GetCharsetEncoding() => Encoding.UTF8;

        public ContentEncodingType GetContentEncodingType() => ContentEncodingType.GZip;

        public Task<Stream> GetStreamAsync()
        {
            var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                gzip.Write(bytes, 0, bytes.Length);
            }

            compressed.Position = 0;
            return Task.FromResult<Stream>(compressed);
        }
    }

    private class DisposingApiRequest(Uri endpoint, string body) : TestApiRequest(endpoint, responseContent: body)
    {
        public AgentlessConfigurationSource? Source { get; set; }

        public override Task<IApiResponse> GetAsync()
        {
            var response = base.GetAsync();
            // Simulate a shutdown arriving after the request completes but before ApplyAsync.
            Source?.Dispose();
            return response;
        }
    }
}
