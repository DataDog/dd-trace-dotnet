// <copyright file="RaspModuleDownstreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests.RASP;

[Collection(nameof(SecuritySequentialTests))]
public class RaspModuleDownstreamTests : WafLibraryRequiredTest
{
    [Fact]
    public void ExtractHeaders_ValidHeaders_ExtractsCorrectly()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Authorization"] = "Bearer token123",
            ["X-Custom-Header"] = "custom-value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().NotBeNull();
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
        result.Should().ContainKey("x-custom-header");
    }

    [Fact]
    public void ExtractHeaders_CookieHeader_ExcludesCookie()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Cookie"] = "session=abc123; user=john",
            ["Authorization"] = "Bearer token123"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().NotBeNull();
        result.Should().NotContainKey("cookie");
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
    }

    [Fact]
    public void ExtractHeaders_EmptyHeaders_ReturnsNull()
    {
        var headers = new Dictionary<string, string>();

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHeaders_OnlyCookieHeader_ReturnsNull()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cookie"] = "session=abc123"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHeaders_CaseInsensitiveCookie_ExcludesCookie()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["COOKIE"] = "session=abc123",
            ["CoOkIe"] = "another=value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().NotBeNull();
        result.Should().NotContainKey("cookie");
        result.Should().ContainKey("content-type");
    }

    [Fact]
    public void ExtractHeaders_HeadersToLowercase_ConvertsKeys()
    {
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["AUTHORIZATION"] = "Bearer token123",
            ["X-Custom-Header"] = "value"
        };

        var headersMock = HttpMocks.CreateMockHeaders(headers);
        var result = RaspModule.ExtractHeaders(headersMock);

        result.Should().NotBeNull();
        result.Should().ContainKey("content-type");
        result.Should().ContainKey("authorization");
        result.Should().ContainKey("x-custom-header");
    }

    [Theory]
    [InlineData("{\"key\":\"value\"}", "application/json", true)]
    [InlineData("{\"user\":{\"name\":\"John\"}}", "application/json", true)]
    [InlineData("[1,2,3,4,5]", "application/json", true)]
    [InlineData("", "application/json", false)]
    [InlineData("{\"key\":\"value\"}", "text/plain", false)]
    public async Task AddBody_JsonContent_ParsesCorrectly(string body, string contentType, bool shouldParse)
    {
        var mockContent = HttpMocks.CreateMockContent(body, contentType, body.Length);
        var wafArgs = new Dictionary<string, object>();

        await RaspModule.AddBody(mockContent, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L);

        if (shouldParse && !string.IsNullOrEmpty(body))
        {
            wafArgs.Should().ContainKey(AddressesConstants.DownstreamRequestBody);
        }
        else
        {
            // Empty body should not add to wafArgs
            if (string.IsNullOrEmpty(body))
            {
                wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
            }
        }
    }

    [Fact]
    public async Task AddBody_OversizedContent_SkipsBodyParsing()
    {
        var largeBody = new string('a', 100_000);
        var mockContent = HttpMocks.CreateMockContent(largeBody, "application/json", 100_000);
        var wafArgs = new Dictionary<string, object>();

        // Body size limit is 50,000 bytes
        await RaspModule.AddBody(mockContent, wafArgs, AddressesConstants.DownstreamRequestBody, 50_000L);

        // Should not add body because it exceeds size limit
        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public async Task AddBody_NullContent_DoesNotAddBody()
    {
        var wafArgs = new Dictionary<string, object>();

        await RaspModule.AddBody(null, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public async Task AddBody_ZeroLengthContent_DoesNotAddBody()
    {
        var mockContent = HttpMocks.CreateMockContent(string.Empty, "application/json", 0);
        var wafArgs = new Dictionary<string, object>();

        await RaspModule.AddBody(mockContent, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public async Task AddBody_InvalidJson_DoesNotAddBody()
    {
        var invalidJson = "{invalid: json}";
        var mockContent = HttpMocks.CreateMockContent(invalidJson, "application/json");
        var wafArgs = new Dictionary<string, object>();

        await RaspModule.AddBody(mockContent, wafArgs, AddressesConstants.DownstreamRequestBody, 10_000_000L);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamRequestBody);
    }

    [Fact]
    public async Task AddBody_JsonContent_DoesNotPreventSubsequentContentRead()
    {
        var body = "{\"key\":\"value\"}";
        var mockContent = HttpMocks.CreateMockContent(body, "application/json", Encoding.UTF8.GetByteCount(body));
        var wafArgs = new Dictionary<string, object>();

        await mockContent.LoadIntoBufferAsync(Encoding.UTF8.GetByteCount(body));
        var stream = await mockContent.ReadAsStreamAsync();
        stream.CanSeek.Should().BeTrue();
        stream.Position.Should().Be(0);

        await RaspModule.AddBody(mockContent, wafArgs, AddressesConstants.DownstreamResponseBody, 10_000_000L);

        wafArgs.Should().ContainKey(AddressesConstants.DownstreamResponseBody);
        stream.Position.Should().Be(0);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);
        var rereadBody = await reader.ReadToEndAsync();
        rereadBody.Should().Be(body);
    }

    [Fact]
    public async Task AddBody_NonSeekableJsonContent_DoesNotPreventSubsequentContentRead()
    {
        var body = "{\"key\":\"value\"}";
        var bodyLength = Encoding.UTF8.GetByteCount(body);
        var wafArgs = new Dictionary<string, object>();

        using var unbufferedContent = HttpMocks.CreateMockContent(body, "application/json", bodyLength, nonSeekable: true);
        var unbufferedStream = await unbufferedContent.ReadAsStreamAsync();
        unbufferedStream.CanSeek.Should().BeFalse();

        using var content = HttpMocks.CreateMockContent(body, "application/json", bodyLength, nonSeekable: true);

        await RaspModule.AddBody(content, wafArgs, AddressesConstants.DownstreamResponseBody, 10_000_000L);

        wafArgs.Should().ContainKey(AddressesConstants.DownstreamResponseBody);

        var bufferedStream = await content.ReadAsStreamAsync();
        bufferedStream.CanSeek.Should().BeTrue();
        bufferedStream.Position.Should().Be(0);

        using var reader = new StreamReader(
            bufferedStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);
        var rereadBody = await reader.ReadToEndAsync();
        rereadBody.Should().Be(body);
    }

    [Theory]
    [InlineData(500, 1_000L)]
    [InlineData(1_000, 1_000L)]
    [InlineData(10_000, 1_000L)]
    public async Task AddBody_ChunkedEncoding_SkipsBody(int sizeInBytes, long bodySizeLimit)
    {
        var chunkedContent = HttpMocks.CreateLargeChunkedContent(sizeInBytes: sizeInBytes, "application/json");
        var wafArgs = new Dictionary<string, object>();

        await RaspModule.AddBody(chunkedContent, wafArgs, AddressesConstants.DownstreamResponseBody, bodySizeLimit);

        wafArgs.Should().NotContainKey(AddressesConstants.DownstreamResponseBody);
    }

    [Theory]
    [InlineData(SpanTypes.Custom, false)]
    [InlineData(SpanTypes.Web, true)]
    public async Task GivenSsrfIsNotInTheRuleset_WhenADownstreamRequestIsChecked_ThenNothingIsReported(string spanType, bool finished)
    {
        // a ruleset without SSRF rules must not report skips for an instrumentation that is not
        // active: these are the lifecycles that report before CheckVulnerability rechecks the address
        var metrics = await CheckDownstreamRequestAsync(ssrfAddressEnabled: false, CreateRootSpan(spanType, finished));

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GivenNoSecurityCoordinator_WhenADownstreamRequestIsChecked_ThenAnOutOfRequestSkipIsReported()
    {
        // no ambient HttpContext, so the coordinator cannot be built and the WAF is never reached
        var metrics = await CheckDownstreamRequestAsync(ssrfAddressEnabled: true, CreateRootSpan(SpanTypes.Web, finished: false));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Equal("reason:out-of-request", "rule_type:ssrf");
    }

    [Fact]
    public async Task GivenANonWebRootSpan_WhenADownstreamRequestIsChecked_ThenAnOutOfRequestSkipIsReported()
    {
        var metrics = await CheckDownstreamRequestAsync(ssrfAddressEnabled: true, CreateRootSpan(SpanTypes.Custom, finished: false));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Equal("reason:out-of-request", "rule_type:ssrf");
    }

    [Fact]
    public async Task GivenAFinishedRootSpan_WhenADownstreamRequestIsChecked_ThenAnAfterRequestSkipIsReported()
    {
        var metrics = await CheckDownstreamRequestAsync(ssrfAddressEnabled: true, CreateRootSpan(SpanTypes.Web, finished: true));

        var tags = metrics.Should().ContainSingle(m => m.Name == "rasp.rule.skipped").Which.Tags;
        tags.Should().Equal("reason:after-request", "rule_type:ssrf");
    }

    private static Span CreateRootSpan(string spanType, bool finished)
    {
        var traceContext = new TraceContext(new EmptyDatadogTracer());
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
        var span = new Span(spanContext, DateTimeOffset.UtcNow) { Type = spanType };
        traceContext.AddSpan(span);

        if (finished)
        {
            span.Finish();
        }

        return span;
    }

    private static AppSecSecurity CreateSecurity(bool ssrfAddressEnabled)
    {
        var waf = new Mock<IWaf>();
        waf.SetupGet(x => x.Version).Returns("1.26.0");
        waf.Setup(x => x.IsKnowAddressesSuported()).Returns(true);
        waf.Setup(x => x.GetKnownAddresses())
           .Returns(ssrfAddressEnabled ? [AddressesConstants.DownstreamUrl] : [AddressesConstants.FileAccess]);

        var config = new NameValueCollection
        {
            { ConfigurationKeys.AppSec.Enabled, "1" },
            { ConfigurationKeys.AppSec.RaspEnabled, "1" },
        };

        var settings = new SecuritySettings(new NameValueConfigurationSource(config), NullConfigurationTelemetry.Instance);

        // passing a waf keeps the real init out of the way, but AppsecEnabled is only flipped by that
        // init, so the configuration state has to be built by hand
        var configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: false) { AppsecEnabled = true };

        return new AppSecSecurity(settings, waf.Object, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: configurationState);
    }

    private static async Task<List<(string Name, string[] Tags)>> CheckDownstreamRequestAsync(bool ssrfAddressEnabled, Span rootSpan)
    {
        var previousSecurity = AppSecSecurity.Instance;
        var security = CreateSecurity(ssrfAddressEnabled);
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);

        try
        {
            AppSecSecurity.Instance = security;

            // OnSSRF arms the thread-static flag OnDownstreamRequest checks, so both calls have to
            // stay on the same thread: no await between them
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://downstream.example.com/");
            RaspModule.OnSSRF(request.RequestUri!.ToString());
            RaspModule.OnDownstreamRequest(request, requestSpanId: 1, rootSpan);
        }
        finally
        {
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
            AppSecSecurity.Instance = previousSecurity;
            security.Dispose();
        }

        await collector.DisposeAsync();

        return collector.GetMetrics().Metrics?
                        .Select(m => (m.Metric, m.Tags ?? []))
                        .ToList()
            ?? [];
    }
}

#endif
