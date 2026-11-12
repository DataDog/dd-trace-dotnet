// <copyright file="AgentlessConfigurationSourceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Moq;
using Xunit;

using IgnoredReason = Datadog.Trace.Telemetry.Metrics.MetricTags.FlagSelectionIgnoredReason;

namespace Datadog.Trace.Tests.FeatureFlags;

public class AgentlessConfigurationSourceTests
{
    private const string Body = """
        { "data": { "type": "universal-flag-configuration",
                    "attributes": { "format": "SERVER", "createdAt": "2025-01-01T00:00:00Z",
                                    "environment": { "name": "production" }, "flags": {} } } }
        """;

    private const string EndpointUrl = "https://ufc-server.ff-cdn.datadoghq.com/api/v2/feature-flagging/config/rules-based/server";

    private static readonly FlagSelection Mobile = FlagSelection.Create(["mobile"], null);

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

    [Fact]
    public async Task ReportsTheNormalizedEnvironmentThatWasRequested()
    {
        var logger = CreateDebugLogger();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: BodyFor("configured-env")));
        using var source = CreateSource(factory, [], environment: "Production", logger: logger.Object);

        await source.PollAsync();

        // The raw setting is normalized before it is sent, so reporting it as written would show a
        // mismatch with the payload's environment that does not exist.
        LoggedValues(logger).Should().Contain("production").And.NotContain("Production");
    }

    [Fact]
    public async Task ReportsTheEnvironmentOfTheRequestThatProducedTheConfiguration()
    {
        var logger = CreateDebugLogger();
        AgentlessConfigurationSource? sourceRef = null;
        var factory = new TestRequestFactory(uri => new EnvironmentChangingApiRequest(uri, BodyFor("configured-env")) { Source = sourceRef });
        using var source = CreateSource(factory, [], environment: "production", logger: logger.Object);
        sourceRef = source;

        await source.PollAsync();

        // The environment changed to staging while the request was in flight, but the response is
        // still the configuration served for the environment that request sent.
        LoggedValues(logger).Should().Contain("production");
    }

    [Fact]
    public async Task ReportsUsableAndRejectedFlagsWithTheConfigurationIdentity()
    {
        var logger = CreateDebugLogger();
        const string flags = """
            "flags": {
                "valid-flag": { "key": "valid-flag", "enabled": true, "variationType": "BOOLEAN", "variations": {}, "allocations": [] },
                "broken-flag": { "key": "broken-flag", "enabled": true, "variationType": "BOOLEAN", "variations": {}, "allocations": [ { "key": "a1" } ] } }
            """;
        var body = Body.Replace("\"flags\": {}", flags);
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }));
        using var source = CreateSource(factory, [], environment: "production", logger: logger.Object);

        await source.PollAsync();

        ArgumentsOf(logger, "applied successfully").Should().Equal(1, 1, "production", "production", "2025-01-01T00:00:00Z", "\"ufc-v1\"");
        ArgumentsOf(logger, "were rejected").Should().Equal(1, "agentless", "broken-flag");
    }

    [Fact]
    public async Task ReportsThatTheAppliedConfigurationRemainsActiveWhenARefreshFails()
    {
        var logger = CreateDebugLogger();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, responseContent: Body),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"),
            uri => new TestApiRequest(uri, statusCode: 500, responseContent: "error"));
        using var source = CreateSource(factory, [], logger: logger.Object);

        await source.PollAsync();
        await source.PollAsync();
        await source.PollAsync();

        LoggedValues(logger).OfType<string>()
                            .Where(v => v.Contains("configuration"))
                            .Should().ContainInOrder(
                                 "no configuration has been applied yet, so evaluations return their default values",
                                 "the previously applied configuration remains active");
    }

    [Theory]
    [InlineData("https://user:token@example.com/signed-token/ufc?sig=secret", "https://example.com")]
    [InlineData("http://localhost:8080/path", "http://localhost:8080")]
    [InlineData(EndpointUrl, "https://ufc-server.ff-cdn.datadoghq.com")]
    public void RedactedOriginKeepsOnlySchemeHostAndPort(string url, string expected)
        => AgentlessConfigurationSource.RedactedOrigin(new Uri(url)).Should().Be(expected);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsEmptyAndAllInvalidConfigurations(bool invalid)
    {
        var logger = CreateDebugLogger();
        var body = invalid ? Body.Replace("\"flags\": {}", "\"flags\": {\"broken\": null}") : Body;
        var applied = new List<ServerConfiguration>();
        using var source = CreateSource(new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: body)), applied, logger: logger.Object);

        await source.PollAsync();

        applied.Should().ContainSingle();
        ArgumentsOf(logger, "applied successfully").Take(2).Should().Equal(0, invalid ? 1 : 0);
    }

    [Fact]
    public async Task ReportsTheRequestTimeoutAndEveryFailedAttempt()
    {
        const string url = "http://localhost/config";
        var lines = new List<string>();
        var sink = new Mock<ILogEventSink>();
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(e => lines.Add(e.RenderMessage() + e.Exception));
        using var serilog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink.Object).CreateLogger();
        var logger = new DatadogSerilogLogger(serilog, new NullLogRateLimiter(), null);
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl] = url,
        }));
        using var configuredSource = AgentlessConfigurationSource.Create(settings.FeatureFlags, settings.Manager, (_, _) => ConfigurationApplyResult.Applied, logger);
        AgentlessEndpoint.TryCreate(null, url, out var endpoint, out _).Should().BeTrue();
        var factory = new TestRequestFactory(_ => throw new IOException("connection reset"));
        using var source = new AgentlessConfigurationSource(endpoint!, factory, TimeSpan.FromSeconds(30), (_, _) => ConfigurationApplyResult.Applied, waitAsync: NoWait, logger: logger);

        await source.PollAsync();

        string.Join("\n", lines).Should().Contain("request timeout=5s")
              .And.Contain("IOException: connection reset")
              .And.Contain("http://localhost").And.Contain("after 3 attempt(s)");
    }

    [Fact]
    public async Task SendsTheFlagSelection()
    {
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, [], environment: "production", selection: FlagSelection.Create(["mobile"], ["checkout.*"]));

        await source.PollAsync();

        factory.RequestsSent[0].Endpoint.AbsoluteUri.Should().Be(EndpointUrl + "?dd_env=production&selected_flag_tags=mobile&selected_flag_key_patterns=checkout.%2A");
        source.OmitsFlagSelection.Should().BeFalse();
    }

    [Fact]
    public async Task LeavesOutASelectionThatWouldMakeTheRequestUrlTooLong()
    {
        var selection = AgentlessEndpointTests.SelectionForUrlLength(CreateEndpoint(), null, 8193);
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, [], selection: selection);

        await source.PollAsync();

        source.OmitsFlagSelection.Should().BeTrue();
        factory.RequestsSent[0].Endpoint.Should().Be(new Uri(EndpointUrl));
    }

    [Fact]
    public async Task KeepsTheSelectionWhenTheEnvironmentChanges()
    {
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, [], environment: "production", selection: Mobile);

        source.UpdateEnvironment("staging");
        await source.PollAsync();

        factory.RequestsSent[0].Endpoint.AbsoluteUri.Should().Be(EndpointUrl + "?dd_env=staging&selected_flag_tags=mobile");
    }

    [Fact]
    public async Task RequestsANewlyClaimedSelectionWithoutTheOldEtag()
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }));
        using var source = CreateSource(factory, []);
        await source.PollAsync();

        source.UpdateFlagSelection(Mobile);
        await source.PollAsync();

        factory.RequestsSent[1].Endpoint.AbsoluteUri.Should().Be(EndpointUrl + "?selected_flag_tags=mobile");
        factory.RequestsSent[1].ExtraHeaders.Should().NotContainKey("If-None-Match", "the ETag identifies the configuration of a different selection");
    }

    [Fact]
    public async Task RequestsANewlyClaimedSelectionWithoutTheEtagWhenTheUriIsUnchanged()
    {
        Func<Uri, TestApiRequest> response = uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } });
        var factory = new TestRequestFactory(response, response, response);
        using var source = CreateSource(factory, []);
        await source.PollAsync();

        // Dropped for a tag over 200 bytes, so nothing is sent and the URI stays the same.
        source.UpdateFlagSelection(FlagSelection.Create([new string('a', 201)], null));
        await source.PollAsync();
        await source.PollAsync();

        factory.RequestsSent[1].Endpoint.Should().Be(factory.RequestsSent[0].Endpoint);
        factory.RequestsSent[1].ExtraHeaders.Should().NotContainKey("If-None-Match");
        factory.RequestsSent[2].ExtraHeaders.Should().ContainKey("If-None-Match", "only the request after the claim goes out without it");
    }

    [Fact]
    public async Task KeepsTheEtagWhenAClaimRequestsTheSameSelection()
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }));
        using var source = CreateSource(factory, [], selection: Mobile);
        await source.PollAsync();

        source.UpdateFlagSelection(FlagSelection.Create([" mobile", "mobile"], []));
        await source.PollAsync();

        factory.RequestsSent[1].Endpoint.Should().Be(factory.RequestsSent[0].Endpoint);
        factory.RequestsSent[1].ExtraHeaders["If-None-Match"].Should().Be("\"ufc-v1\"");
    }

    [Fact]
    public async Task ReportsAnIgnoredSelectionOnceThePayloadIsApplied()
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var logger = new Mock<IDatadogLogger>();
        var reportedAtEachApply = new List<int>();
        Func<Uri, TestApiRequest> ignoredResponse = uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: IgnoredHeader("tag_load_failed"));
        var factory = new TestRequestFactory(ignoredResponse, ignoredResponse);
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) =>
            {
                reportedAtEachApply.Add(FlagSelectionTests.Errors(logger).Count);
                return ConfigurationApplyResult.Applied;
            },
            waitAsync: NoWait,
            logger: logger.Object,
            flagSelection: Mobile,
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(metrics.Object));

        await source.PollAsync();
        await source.PollAsync();

        // Nothing is reported before the first configuration is applied, and the second response, which
        // says the same, is not reported again.
        reportedAtEachApply.Should().Equal(0, 1);
        FlagSelectionTests.Errors(logger).Should().Equal(FlagSelectionDiagnostics.TagLoadFailedIgnored);
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(IgnoredReason.TagLoadFailed, 1), Times.Once);
    }

    [Theory]
    [InlineData("unexpected_reason")]
    [InlineData("too_many_values ")]
    [InlineData("too_many_values, invalid_tag")]
    [InlineData("a-reason-that-is-longer-than-32-bytes")]
    public async Task ReportsAnUnrecognizedIgnoredReasonAsUnknown(string header)
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: IgnoredHeader(header)));
        using var source = CreateSource(factory, [], logger: logger.Object, selection: Mobile, diagnostics: new FlagSelectionDiagnostics(metrics.Object));

        await source.PollAsync();

        FlagSelectionTests.Errors(logger).Should().Equal(FlagSelectionDiagnostics.UnknownIgnored);
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(IgnoredReason.Unknown, 1), Times.Once);
        LoggedValues(logger).OfType<string>().Should().NotContain(v => v.Contains(header));
    }

    [Fact]
    public async Task TreatsAnEmptyIgnoredHeaderAsAbsent()
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: IgnoredHeader(string.Empty)));
        using var source = CreateSource(factory, [], logger: logger.Object, selection: Mobile, diagnostics: new FlagSelectionDiagnostics(metrics.Object));

        await source.PollAsync();

        FlagSelectionTests.Errors(logger).Should().BeEmpty();
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(It.IsAny<IgnoredReason>(), It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData(304)]
    [InlineData(400)]
    [InlineData(500)]
    public async Task ReadsTheIgnoredHeaderOnlyOnA200(int statusCode)
    {
        var readHeaders = new List<string>();
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new HeaderRecordingApiRequest(uri, statusCode, IgnoredHeader("invalid_tag"), readHeaders));
        using var source = CreateSource(factory, [], logger: logger.Object, selection: Mobile, diagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        await source.PollAsync();

        readHeaders.Should().NotContain(FlagSelectionDiagnostics.IgnoredHeaderName);
        FlagSelectionTests.Errors(logger).Should().BeEmpty();
    }

    [Theory]
    [CombinatorialData]
    public async Task DoesNotReadTheIgnoredHeaderWhenNoSelectionWasSent(bool omittedForLength)
    {
        var readHeaders = new List<string>();
        var logger = new Mock<IDatadogLogger>();
        var selection = omittedForLength ? AgentlessEndpointTests.SelectionForUrlLength(CreateEndpoint(), null, 8193) : FlagSelection.AllFlags;
        var factory = new TestRequestFactory(uri => new HeaderRecordingApiRequest(uri, 200, IgnoredHeader("invalid_tag"), readHeaders));
        using var source = CreateSource(factory, [], logger: logger.Object, selection: selection, diagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        await source.PollAsync();

        readHeaders.Should().Contain("ETag").And.NotContain(FlagSelectionDiagnostics.IgnoredHeaderName);
        FlagSelectionTests.Errors(logger).Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReportAnIgnoredSelectionWhenThePayloadIsNotApplied()
    {
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: IgnoredHeader("invalid_tag")));
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) => ConfigurationApplyResult.Failed,
            waitAsync: NoWait,
            logger: logger.Object,
            flagSelection: Mobile,
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        await source.PollAsync();

        FlagSelectionTests.Errors(logger).Should().BeEmpty();
    }

    [Fact]
    public async Task ReportsTheUrlLimitOnceWhenAnEnvironmentChangeMakesTheRequestTooLong()
    {
        var endpoint = CreateEndpoint();
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, statusCode: 304));
        var polled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new AgentlessConfigurationSource(
            endpoint,
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) => ConfigurationApplyResult.Applied,
            environment: "a",
            waitAsync: _ =>
            {
                polled.TrySetResult(true);
                return new TaskCompletionSource<bool>().Task;
            },
            logger: logger.Object,
            flagSelection: AgentlessEndpointTests.SelectionForUrlLength(endpoint, "a", 8192 - 50),
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        // Before the source starts, the module reports what activation finds.
        source.UpdateEnvironment(new string('b', 100));
        source.UpdateEnvironment("a");
        FlagSelectionTests.Errors(logger).Should().BeEmpty();

        source.Start();
        (await Task.WhenAny(polled.Task, Task.Delay(TimeSpan.FromSeconds(30)))).Should().Be(polled.Task);
        source.OmitsFlagSelection.Should().BeFalse();

        source.UpdateEnvironment(new string('b', 100));
        source.UpdateEnvironment(new string('c', 100));

        source.OmitsFlagSelection.Should().BeTrue();
        FlagSelectionTests.Errors(logger).Should().Equal(FlagSelectionDiagnostics.UrlLimitNotSent);
    }

    [Fact]
    public async Task RequestsANewlyClaimedSelectionWithoutWaitingForThePollInterval()
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 304),
            uri => new TestApiRequest(uri, statusCode: 304));
        var waited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) => ConfigurationApplyResult.Applied,
            waitAsync: _ =>
            {
                waited.TrySetResult(true);
                return new TaskCompletionSource<bool>().Task;
            },
            flagSelection: Mobile,
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        source.Start();
        (await Task.WhenAny(waited.Task, Task.Delay(TimeSpan.FromSeconds(30)))).Should().Be(waited.Task);

        source.UpdateFlagSelection(FlagSelection.Create(["beta"], null));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (factory.RequestsSent.Count < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        factory.RequestsSent.Should().HaveCount(2, "the poll interval never elapses in this test");
        factory.RequestsSent[1].Endpoint.AbsoluteUri.Should().Be(EndpointUrl + "?selected_flag_tags=beta");
    }

    [Fact]
    public async Task ANewlyClaimedSelectionEndsARetryDelayAndIsRequestedOnce()
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, statusCode: 503),
            uri => new TestApiRequest(uri, statusCode: 304),
            uri => new TestApiRequest(uri, statusCode: 304));
        var waits = 0;
        var firstWait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) => ConfigurationApplyResult.Applied,
            waitAsync: _ =>
            {
                System.Threading.Interlocked.Increment(ref waits);
                firstWait.TrySetResult(true);
                return new TaskCompletionSource<bool>().Task;
            },
            flagSelection: Mobile,
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        source.Start();
        (await Task.WhenAny(firstWait.Task, Task.Delay(TimeSpan.FromSeconds(30)))).Should().Be(firstWait.Task, "the 503 starts a retry delay");

        source.UpdateFlagSelection(FlagSelection.Create(["beta"], null));

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (System.Threading.Volatile.Read(ref waits) < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        await Task.Delay(200);
        factory.RequestsSent.Should().HaveCount(2, "the claim ends the retry delay, and the poll interval never elapses in this test");
        factory.RequestsSent[1].Endpoint.AbsoluteUri.Should().Be(EndpointUrl + "?selected_flag_tags=beta");
    }

#if NETCOREAPP3_1_OR_GREATER
    [Theory]
    [InlineData("too_many_values", "invalid_tag")]
    [InlineData("too_many_values", "too_many_values")]
    [InlineData("", "invalid_tag")]
    public async Task ReportsRepeatedIgnoredHeadersAsUnknown(string first, string second)
    {
        var metrics = new Mock<IMetricsTelemetryCollector>();
        var logger = new Mock<IDatadogLogger>();
        var factory = new TestRequestFactory(uri => new HttpClientApiRequest(uri, Body, first, second));
        using var source = CreateSource(factory, [], logger: logger.Object, selection: Mobile, diagnostics: new FlagSelectionDiagnostics(metrics.Object));

        await source.PollAsync();

        FlagSelectionTests.Errors(logger).Should().Equal(FlagSelectionDiagnostics.UnknownIgnored);
        metrics.Verify(m => m.RecordCountFlagSelectionIgnored(IgnoredReason.Unknown, 1), Times.Once);
    }

#endif
    [Fact]
    public async Task ASupersededResponseLeavesTheEtagAndReportsNothing()
    {
        var logger = new Mock<IDatadogLogger>();
        Func<Uri, TestApiRequest> response = uri => new TestApiRequest(
            uri,
            responseContent: Body,
            responseHeaders: new() { { "ETag", "\"ufc-v1\"" }, { FlagSelectionDiagnostics.IgnoredHeaderName, "tag_load_failed" } });
        var factory = new TestRequestFactory(response, response);
        var requested = new List<FlagSelection>();
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, selection) =>
            {
                requested.Add(selection);
                return ConfigurationApplyResult.Superseded;
            },
            waitAsync: NoWait,
            logger: logger.Object,
            flagSelection: Mobile,
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        await source.PollAsync();
        await source.PollAsync();

        requested.Should().Equal(Mobile, Mobile);
        factory.RequestsSent[1].ExtraHeaders.Should().NotContainKey("If-None-Match");
        FlagSelectionTests.Errors(logger).Should().BeEmpty();
        logger.Invocations.Should().NotContain(i => i.Method.Name == nameof(IDatadogLogger.Warning));
    }

    [Fact]
    public async Task FailureLogsNeverIncludeTheSelectedValues()
    {
        var lines = new List<string>();
        var sink = new Mock<ILogEventSink>();
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(e => lines.Add(e.RenderMessage() + e.Exception));
        using var serilog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink.Object).CreateLogger();
        var logger = new DatadogSerilogLogger(serilog, new NullLogRateLimiter(), null);

        // The .NET Framework transport names the request URI when a request times out.
        var factory = new TestRequestFactory(
            uri => throw new OperationCanceledException($"The request to {uri} timed out after 5000ms.", new IOException($"Failed to read {uri.PathAndQuery}")));
        using var source = new AgentlessConfigurationSource(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (_, _) => ConfigurationApplyResult.Applied,
            waitAsync: NoWait,
            logger: logger,
            flagSelection: FlagSelection.Create(["secret team"], ["secret-*"]),
            flagSelectionDiagnostics: new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

        await source.PollAsync();

        var output = string.Join("\n", lines);
        output.Should().Contain("OperationCanceledException: The request to https://").And.Contain("after 3 attempt(s)");
        output.Should().NotContain("secret").And.NotContain("selected_flag");
    }

    [Theory]
    [InlineData("https://ff-cdn.datadoghq.com/rules?selected_flag_tags=secret%20team&selected_flag_key_patterns=secret-%2A")]
    [InlineData("http://user:token@proxy.internal:8080/flags?sig=abc&selected_flag_tags=secret")]
    public void RedactUri_RemovesTheQueryInEveryFormATransportPrints(string url)
    {
        var uri = new Uri(url);
        var message = $"a {uri} b {uri.AbsoluteUri} c {uri.OriginalString} d {uri.PathAndQuery} e {Uri.UnescapeDataString(uri.PathAndQuery)}";

        var redacted = AgentlessConfigurationSource.RedactUri(message, uri);

        redacted.Should().NotContain("secret").And.NotContain("token").And.NotContain("sig=");
        redacted.Should().Contain(AgentlessConfigurationSource.RedactedOrigin(uri));
    }

    private static Dictionary<string, string> IgnoredHeader(string value) => new() { { FlagSelectionDiagnostics.IgnoredHeaderName, value } };

    private static AgentlessConfigurationSource CreateSource(
        TestRequestFactory factory,
        List<ServerConfiguration> applied,
        string? environment = null,
        IDatadogLogger? logger = null,
        FlagSelection? selection = null,
        FlagSelectionDiagnostics? diagnostics = null)
        => new(
            CreateEndpoint(),
            factory,
            TimeSpan.FromSeconds(30),
            (configuration, _) =>
            {
                applied.Add(configuration);
                return ConfigurationApplyResult.Applied;
            },
            environment,
            waitAsync: NoWait,
            logger: logger,
            flagSelection: selection,
            flagSelectionDiagnostics: diagnostics ?? new FlagSelectionDiagnostics(new Mock<IMetricsTelemetryCollector>().Object));

    private static Mock<IDatadogLogger> CreateDebugLogger()
    {
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        return logger;
    }

    // Every value handed to the logger, whatever the message says, so these tests survive rewording.
    // A call taking an argument array contributes its elements.
    private static List<object?> LoggedValues(Mock<IDatadogLogger> logger)
        => logger.Invocations
                 .SelectMany(i => i.Method.GetParameters().Zip(i.Arguments, (parameter, argument) => (parameter.Name, argument)))
                 .Where(p => p.Name is not ("sourceLine" or "sourceFile"))
                 .SelectMany(p => p.argument is object?[] args ? args : [p.argument])
                 .ToList();

    // The values handed to the single log call whose message contains the fragment.
    private static List<object?> ArgumentsOf(Mock<IDatadogLogger> logger, string templateFragment)
        => logger.Invocations
                 .Select(i => i.Method.GetParameters().Zip(i.Arguments, (parameter, argument) => (parameter.Name, argument)).ToList())
                 .Where(call => call.Any(p => p.Name == "messageTemplate" && ((string)p.argument!).Contains(templateFragment)))
                 .Select(call => call.Where(p => p.Name is not ("sourceLine" or "sourceFile" or "messageTemplate"))
                                     .SelectMany(p => p.argument is object?[] args ? args : [p.argument])
                                     .ToList())
                 .Should().ContainSingle().Subject;

    private static string BodyFor(string environmentName) => Body.Replace("\"production\"", $"\"{environmentName}\"");

    private static AgentlessEndpoint CreateEndpoint()
    {
        AgentlessEndpoint.TryCreate("datadoghq.com", baseUrl: null, out var endpoint, out _).Should().BeTrue();
        return endpoint ?? throw new InvalidOperationException("TryCreate reported success without producing an endpoint.");
    }

    private static Task NoWait(TimeSpan delay) => Task.CompletedTask;

#if NETCOREAPP3_1_OR_GREATER
    private class HttpClientApiRequest(Uri endpoint, string body, params string[] ignoredHeaderValues) : TestApiRequest(endpoint)
    {
        public override Task<IApiResponse> GetAsync()
        {
            var message = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(body) };
            foreach (var value in ignoredHeaderValues)
            {
                message.Headers.TryAddWithoutValidation(FlagSelectionDiagnostics.IgnoredHeaderName, value);
            }

            return Task.FromResult<IApiResponse>(new Datadog.Trace.Agent.Transports.HttpClientResponse(message));
        }
    }

#endif
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

    // Records every response header the source reads.
    private class HeaderRecordingApiRequest(Uri endpoint, int statusCode, Dictionary<string, string> headers, List<string> readHeaders)
        : TestApiRequest(endpoint, statusCode, Body, responseHeaders: headers)
    {
        public override async Task<IApiResponse> GetAsync() => new HeaderRecordingApiResponse(await base.GetAsync(), readHeaders);
    }

    private class HeaderRecordingApiResponse(IApiResponse inner, List<string> readHeaders) : IApiResponse
    {
        public int StatusCode => inner.StatusCode;

        public long ContentLength => inner.ContentLength;

        public string? ContentTypeHeader => inner.ContentTypeHeader;

        public string? ContentEncodingHeader => inner.ContentEncodingHeader;

        public void Dispose() => inner.Dispose();

        public string? GetHeader(string headerName)
        {
            readHeaders.Add(headerName);
            return inner.GetHeader(headerName);
        }

        public Encoding GetCharsetEncoding() => inner.GetCharsetEncoding();

        public ContentEncodingType GetContentEncodingType() => inner.GetContentEncodingType();

        public Task<Stream> GetStreamAsync() => inner.GetStreamAsync();
    }

    private class EnvironmentChangingApiRequest(Uri endpoint, string body) : TestApiRequest(endpoint, responseContent: body)
    {
        public AgentlessConfigurationSource? Source { get; set; }

        public override Task<IApiResponse> GetAsync()
        {
            var response = base.GetAsync();
            // Simulate the environment being changed in code while this request is in flight.
            Source?.UpdateEnvironment("staging");
            return response;
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
