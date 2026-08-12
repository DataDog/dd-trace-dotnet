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
using System.Threading;
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

    private static readonly Uri Endpoint = new("https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server");

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
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new Dictionary<string, string> { { "ETag", "\"v1\"" } }),
            uri => new TestApiRequest(uri, statusCode: 304, responseContent: string.Empty));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();
        await source.PollAsync();

        factory.RequestsSent[0].ExtraHeaders.Should().NotContainKey("If-None-Match");
        factory.RequestsSent[1].ExtraHeaders.Should().Contain(new KeyValuePair<string, string>("If-None-Match", "\"v1\""));

        // A 304 is a no-op: last-known-good and the ETag both stay in place.
        applied.Should().ContainSingle();
    }

    [Theory]
    // The ETag advances only after both parsing and applying succeed. Advancing on receipt would
    // acknowledge a payload that was never applied, and the endpoint would answer 304 forever.
    [InlineData("not a UFC document", true)]
    [InlineData(Body, false)]
    public async Task DoesNotAdvanceTheEtagWhenTheConfigurationIsNotApplied(string body, bool applySucceeds)
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: body, responseHeaders: new Dictionary<string, string> { { "ETag", "\"v1\"" } }),
            uri => new TestApiRequest(uri, responseContent: body));
        using var source = new AgentlessConfigurationSource(Endpoint, factory, TimeSpan.FromSeconds(30), _ => applySucceeds, NoWait);

        await source.PollAsync();
        await source.PollAsync();

        factory.RequestsSent[1].ExtraHeaders.Should().NotContainKey("If-None-Match");
    }

    [Fact]
    public async Task DecompressesAGzippedBody()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(uri => new GzipApiRequest(uri, Body));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        applied.Should().ContainSingle();
    }

    [Theory]
    // Retried within the tick.
    [InlineData(408, 3)]
    [InlineData(429, 3)]
    [InlineData(500, 3)]
    [InlineData(503, 3)]
    // Decisive: retrying would not change the answer.
    [InlineData(400, 1)]
    [InlineData(401, 1)]
    [InlineData(403, 1)]
    [InlineData(404, 1)]
    [InlineData(304, 1)]
    [InlineData(200, 1)]
    public async Task RetriesOnlyRetryableStatuses(int statusCode, int expectedRequests)
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(Enumerable
                                            .Range(0, 3)
                                            .Select(_ => (Func<Uri, TestApiRequest>)(uri => new TestApiRequest(uri, statusCode, Body)))
                                            .ToArray());
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        factory.RequestsSent.Should().HaveCount(expectedRequests);

        // Only a 200 carries configuration; other bodies are never decoded as one.
        applied.Should().HaveCount(statusCode == 200 ? 1 : 0);
    }

    [Fact]
    public async Task RetriesOnANetworkError()
    {
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new ThrowingApiRequest(uri),
            uri => new ThrowingApiRequest(uri),
            uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, applied);

        await source.PollAsync();

        factory.RequestsSent.Should().HaveCount(3);
        applied.Should().ContainSingle();
    }

    [Theory]
    // clamp(interval / 6, 2s, 10s) then clamp(interval / 3, 5s, 30s), each with +/-20% jitter.
    [InlineData(30, 4, 6, 8, 12)]
    [InlineData(3600, 8, 12, 24, 36)]
    // A short interval is clamped up to the minimums, so retries never become a burst.
    [InlineData(0.2, 1.6, 2.4, 4, 6)]
    public async Task BacksOffBetweenAttempts(double intervalSeconds, double firstMin, double firstMax, double secondMin, double secondMax)
    {
        var waits = new List<TimeSpan>();
        var factory = new TestRequestFactory(Enumerable
                                            .Range(0, 3)
                                            .Select(_ => (Func<Uri, TestApiRequest>)(uri => new TestApiRequest(uri, statusCode: 500)))
                                            .ToArray());
        using var source = new AgentlessConfigurationSource(
            Endpoint,
            factory,
            TimeSpan.FromSeconds(intervalSeconds),
            _ => true,
            (delay, _) =>
            {
                waits.Add(delay);
                return Task.CompletedTask;
            });

        await source.PollAsync();

        waits.Should().HaveCount(2);
        waits[0].TotalSeconds.Should().BeInRange(firstMin, firstMax);
        waits[1].TotalSeconds.Should().BeInRange(secondMin, secondMax);
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

    private static AgentlessConfigurationSource CreateSource(TestRequestFactory factory, List<ServerConfiguration> applied)
        => new(
            Endpoint,
            factory,
            TimeSpan.FromSeconds(30),
            configuration =>
            {
                applied.Add(configuration);
                return true;
            },
            NoWait);

    private static Task NoWait(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;

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
