// <copyright file="BlockingMiddlewareTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests;

[Collection(nameof(SecuritySequentialTests))]
public class BlockingMiddlewareTests
{
    [Fact]
    public async Task GivenAWafBlock_WhenWritingTheBlockingResponseThrows_ThenTheBlockFailureIsRecorded()
    {
        var (rootSpan, wafRequestsTags) = await InvokeAsync(SecurityReporterWafTelemetryTests.CreateResult(block: true), next: null);

        rootSpan.GetMetric(Metrics.AppSecBlockFailed).Should().Be(1);
        wafRequestsTags.Should().Contain(["request_blocked:false", "block_failure:true"]);
    }

    [Fact]
    public async Task GivenABlockException_WhenWritingTheBlockingResponseThrows_ThenTheBlockFailureIsRecorded()
    {
        var blockingResult = SecurityReporterWafTelemetryTests.CreateResult(block: true);
        var (rootSpan, _) = await InvokeAsync(
                                SecurityReporterWafTelemetryTests.CreateResult(),
                                next: _ => throw new BlockException(blockingResult, new Dictionary<string, object?>()));

        rootSpan.GetMetric(Metrics.AppSecBlockFailed).Should().Be(1);
    }

    // the response start hooks (FireOnStarting) run the response WAF scan when the response is first written, which
    // skips the requests that are already blocked
    [Fact]
    public async Task GivenAWafBlock_WhenTheBlockingResponseStarts_ThenTheRequestIsAlreadyMarkedBlocked()
    {
        var body = new BlockedStateObservingStream();
        await InvokeAsync(SecurityReporterWafTelemetryTests.CreateResult(block: true), next: null, body);

        body.BlockedOnFirstWrite.Should().BeTrue();
    }

    [Fact]
    public async Task GivenABlockException_WhenTheBlockingResponseStarts_ThenTheRequestIsAlreadyMarkedBlocked()
    {
        var blockingResult = SecurityReporterWafTelemetryTests.CreateResult(block: true);
        var body = new BlockedStateObservingStream();
        await InvokeAsync(
            SecurityReporterWafTelemetryTests.CreateResult(),
            next: _ => throw new BlockException(blockingResult, new Dictionary<string, object?>()),
            body);

        body.BlockedOnFirstWrite.Should().BeTrue();
    }

    // runs the middleware in a web request, then ends the request. Without body, writing the response body throws,
    // and so must the middleware; with it, the response is written to body.
    private static async Task<(Span RootSpan, string[] WafRequestsTags)> InvokeAsync(IResult wafResult, RequestDelegate? next, BlockedStateObservingStream? body = null)
    {
        var previousSecurity = AppSecSecurity.Instance;
        var previousTracer = Tracer.Instance;
        var previousTracerManager = previousTracer.TracerManager;
        var security = CreateSecurity(wafResult);
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        var collector = new MetricsTelemetryCollector(Timeout.InfiniteTimeSpan);
        var previousMetrics = TelemetryFactory.SetMetricsForTesting(collector);
        Span rootSpan;

        try
        {
            AppSecSecurity.Instance = security;
            TracerRestorerAttribute.SetTracer(tracer);

            using (var scope = (Scope)tracer.StartActive("aspnet_core.request"))
            {
                rootSpan = scope.Span;
                rootSpan.Type = SpanTypes.Web;
                var context = CreateHttpContext(body ?? (Stream)new ThrowingStream());
                body?.Observe(context);

                var invoke = () => new BlockingMiddleware(next).Invoke(context);

                if (body is null)
                {
                    // exactly, as a FileNotFoundException from an assembly that fails to load is an IOException too
                    await invoke.Should().ThrowExactlyAsync<IOException>().WithMessage(ThrowingStream.Message);
                }
                else
                {
                    await invoke();
                }
            }
        }
        finally
        {
            TracerRestorerAttribute.SetTracer(previousTracer, previousTracerManager);
            TelemetryFactory.SetMetricsForTesting(previousMetrics);
            AppSecSecurity.Instance = previousSecurity;
            security.Dispose();
        }

        await collector.DisposeAsync();
        var wafRequestsTags = collector.GetMetrics().Metrics?.FirstOrDefault(m => m.Metric == "waf.requests")?.Tags ?? [];
        return (rootSpan, wafRequestsTags);
    }

    // a request whose response hasn't started
    private static HttpContext CreateHttpContext(Stream body)
    {
        var headers = new Mock<IHeaderDictionary>();
        headers.Setup(x => x.Keys).Returns([]);

        var requestCookies = new Mock<IRequestCookieCollection>();
        requestCookies.Setup(x => x.GetEnumerator()).Returns(() => new List<KeyValuePair<string, string>>().GetEnumerator());

        var request = new Mock<HttpRequest>();
        request.Setup(x => x.Method).Returns("GET");
        request.Setup(x => x.Path).Returns(new PathString("/"));
        request.Setup(x => x.Headers).Returns(headers.Object);
        request.Setup(x => x.Cookies).Returns(requestCookies.Object);

        var response = new Mock<HttpResponse>();
        response.SetupProperty(x => x.StatusCode, 200);
        response.SetupProperty(x => x.ContentType);
        response.Setup(x => x.HasStarted).Returns(false);
        response.Setup(x => x.Headers).Returns(new Mock<IHeaderDictionary>().Object);
        response.Setup(x => x.Cookies).Returns(new Mock<IResponseCookies>().Object);
        response.Setup(x => x.Body).Returns(body);

        var context = new Mock<HttpContext>();
        context.Setup(x => x.Request).Returns(request.Object);
        context.Setup(x => x.Response).Returns(response.Object);
        context.Setup(x => x.Items).Returns(new Dictionary<object, object?>());
        var features = new FeatureCollection();
        features.Set(new Mock<IHttpResponseFeature>().Object);
        context.Setup(x => x.Features).Returns(features);
        response.Setup(x => x.HttpContext).Returns(context.Object);
        request.Setup(x => x.HttpContext).Returns(context.Object);
        return context.Object;
    }

    private static AppSecSecurity CreateSecurity(IResult wafResult)
    {
        var wafContext = new Mock<IContext>();
        wafContext.Setup(x => x.Run(It.IsAny<IDictionary<string, object>>(), It.IsAny<ulong>())).Returns(wafResult);

        var outcome = WafOutcome.Success;
        var waf = new Mock<IWaf>();
        waf.SetupGet(x => x.Version).Returns("1.26.0");
        waf.Setup(x => x.CreateContext(out outcome, It.IsAny<bool>())).Returns(wafContext.Object);

        var config = new NameValueCollection { { ConfigurationKeys.AppSec.Enabled, "1" } };
        var settings = new SecuritySettings(new NameValueConfigurationSource(config), NullConfigurationTelemetry.Instance);

        // passing a waf keeps the real init out of the way, but AppsecEnabled is only flipped by that init
        var configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: false) { AppsecEnabled = true };

        return new AppSecSecurity(settings, waf.Object, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: configurationState);
    }

    private sealed class ThrowingStream : MemoryStream
    {
        internal const string Message = "Test exception";

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException(Message);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new IOException(Message);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException(Message);

        public override Task FlushAsync(CancellationToken cancellationToken) => throw new IOException(Message);
    }

    // records whether the request is marked blocked when the response is first written
    private sealed class BlockedStateObservingStream : MemoryStream
    {
        private HttpContext? _context;

        public bool? BlockedOnFirstWrite { get; private set; }

        public void Observe(HttpContext context) => _context = context;

        public override void Write(byte[] buffer, int offset, int count)
        {
            RecordFirstWrite();
            base.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            RecordFirstWrite();
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            RecordFirstWrite();
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void RecordFirstWrite()
            => BlockedOnFirstWrite ??= _context is { } context && new SecurityCoordinator.HttpTransport(context).IsBlocked;
    }
}
#endif
