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
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using FluentAssertions;
using Moq;
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

    private const string ServiceRejection = "DD-Service header not sent";

    // Each row is a service and its DD-Service header. Null means that the request has no header.
    // The values come from NormalizeService in the Agent v0.82.3. NormalizeService gives the name
    // APM shows. The source sends that name only when it is ASCII.
    public static TheoryData<string, string?> ServiceHeaders => new()
    {
        { "checkout", "checkout" },
        { "My Service", "my_service" },
        { "My-API", "my-api" },
        { "UPPER", "upper" },
        { "bad\nvalue", "bad_value" },
        { "a\tb", "a_b" },
        { "a  b!", "a_b" },
        { "svc ", "svc" },
        { " svc", "_svc" },
        { "a_", "a" },
        { "svc-", "svc-" },
        { "a..b", "a..b" },
        { "2fa-service", "2fa-service" },
        { "9lives", "9lives" },
        { "unnamed-service", "unnamed-service" },
        { "svc_name.v2:api/edge-1", "svc_name.v2:api/edge-1" },
        { "my__service", "my_service" },
        { "a🐶b", "a_b" },

        // The tracer infers this name for an IIS application in the default site.
        { "Default Web Site/Shop", "default_web_site/shop" },

        // The Agent cuts a service to 100 bytes of UTF-8, and never splits a character.
        { "a" + new string('b', 99), "a" + new string('b', 99) },
        { "a" + new string('b', 100), "a" + new string('b', 99) },
        { new string('a', 100) + " b", new string('a', 100) },
        { new string('a', 99) + "\u00e9", new string('a', 99) },

        // The source sends these names as APM shows them. The CDN decides if it accepts them.
        { "_svc", "_svc" },
        { "-svc", "-svc" },
        { ".svc", ".svc" },
        { ":svc", ":svc" },
        { "-", "-" },

        // APM shows these names, but they are not ASCII. HttpClient refuses them.
        { "caf\u00e9-api", null },
        { "Caf\u00e9", null },
        { "支付", null },
        { "\u0130stanbul", null },
        { new string('\u00e9', 60), null },

        // Nothing is left of these names.
        { "___", null },
        { string.Empty, null },
        { "   ", null },
    };

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
    public async Task SendsTheServiceOnEveryPoll()
    {
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body, responseHeaders: new() { { "ETag", "\"ufc-v1\"" } }),
            uri => new TestApiRequest(uri, statusCode: 304, responseContent: "{}"));
        using var source = CreateSource(factory, [], service: "checkout");

        await source.PollAsync();
        await source.PollAsync();

        factory.RequestsSent.Should().HaveCount(2);
        factory.RequestsSent[0].ExtraHeaders.Should().Contain("DD-Service", "checkout");
        factory.RequestsSent[1].ExtraHeaders.Should().Contain("If-None-Match", "\"ufc-v1\"").And.Contain("DD-Service", "checkout");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OmitsTheServiceWhenThereIsNone(string? service)
    {
        var logger = CreateDebugLogger();
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, [], service: service, logger: logger.Object);

        await source.PollAsync();

        factory.RequestsSent[0].ExtraHeaders.Should().NotContainKey("DD-Service");
        RejectedServices(logger).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(ServiceHeaders))]
    public async Task SendsTheNameApmShowsOrNoHeader(string service, string? header)
    {
        var logger = CreateDebugLogger();
        var applied = new List<ServerConfiguration>();
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body),
            uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateSource(factory, applied, service: service, logger: logger.Object);

        await source.PollAsync();
        await source.PollAsync();

        // Polling is the same with or without the header.
        factory.RequestsSent.Should().HaveCount(2);
        applied.Should().HaveCount(2);

        if (header is not null)
        {
            factory.RequestsSent.Should().AllSatisfy(r => r.ExtraHeaders.Should().Contain("DD-Service", header));
            RejectedServices(logger).Should().BeEmpty();
        }
        else
        {
            factory.RequestsSent.Should().AllSatisfy(r => r.ExtraHeaders.Should().NotContainKey("DD-Service"));

            // The source logs each name that it does not send. It logs the name once, not for each
            // poll. An empty service has no name to log.
            RejectedServices(logger).Should().Equal(service.Length == 0 ? Array.Empty<object?>() : new object?[] { service });
        }
    }

    // The customer sets DD_SERVICE, and the test checks the request that goes to the CDN.
    [Theory]
    [InlineData("My Service", "my_service")]
    [InlineData("Default Web Site/Shop", "default_web_site/shop")]
    [InlineData("_svc", "_svc")]
    [InlineData("caf\u00e9-api", null)]
    public async Task SendsTheNameApmShowsForTheConfiguredServiceAndKeepsTheTracerService(string service, string? header)
    {
        var logger = CreateDebugLogger();
        var settings = CreateTracerSettings(service);
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateConfiguredSource(settings, factory, logger.Object);

        await source.PollAsync();

        // The source sends the request also when the name is not ASCII. That request has no header.
        var headers = factory.RequestsSent.Should().ContainSingle().Subject.ExtraHeaders;
        if (header is not null)
        {
            headers.Should().Contain("DD-Service", header);
            RejectedServices(logger).Should().BeEmpty();
        }
        else
        {
            headers.Should().NotContainKey("DD-Service");
            RejectedServices(logger).Should().Equal(service);
        }

        // Only the header changes. Spans, logs and metrics keep the service as configured.
        settings.Manager.InitialMutableSettings.ServiceName.Should().Be(service);
        settings.Manager.InitialMutableSettings.DefaultServiceName.Should().Be(service);
    }

    [Fact]
    public async Task SendsTheConfiguredServiceAndFollowsChangesMadeInCode()
    {
        var settings = CreateTracerSettings(service: "checkout");
        var factory = new TestRequestFactory(
            uri => new TestApiRequest(uri, responseContent: Body),
            uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateConfiguredSource(settings, factory);

        await source.PollAsync();
        UpdateSettingsInCode(settings, TracerSettingKeyConstants.ServiceNameKey, "Payments API");
        await source.PollAsync();

        factory.RequestsSent[0].ExtraHeaders.Should().Contain("DD-Service", "checkout");
        factory.RequestsSent[1].ExtraHeaders.Should().Contain("DD-Service", "payments_api");
    }

    [Fact]
    public async Task SendsTheInferredServiceWhenNoneIsConfigured()
    {
        var settings = CreateTracerSettings(service: null);
        var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: Body));
        using var source = CreateConfiguredSource(settings, factory);

        await source.PollAsync();

        // The spans use this name when DD_SERVICE is not set. The header sends it as APM shows it.
        // The name of the test host is ASCII, so the source sends it.
        var inferred = settings.Manager.InitialMutableSettings.DefaultServiceName;
        inferred.Should().NotBeNullOrEmpty();
        factory.RequestsSent[0].ExtraHeaders.Should().Contain("DD-Service", AgentlessConfigurationSource.CleanUpService(inferred));
    }

    [Fact]
    public void ReportsARejectedServiceOnlyWhenTheServiceChanges()
    {
        var logger = CreateDebugLogger();
        var settings = CreateTracerSettings(service: "支付");
        using var source = CreateConfiguredSource(settings, new TestRequestFactory(), logger.Object);

        // Every settings change goes to the source. This includes a change that keeps the service.
        UpdateSettingsInCode(settings, TracerSettingKeyConstants.EnvironmentKey, "staging");
        UpdateSettingsInCode(settings, TracerSettingKeyConstants.ServiceNameKey, "caf\u00e9-api");

        RejectedServices(logger).Should().Equal("支付", "caf\u00e9-api");
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
        using var configuredSource = AgentlessConfigurationSource.Create(settings.FeatureFlags, settings.Manager, _ => true, logger);
        AgentlessEndpoint.TryCreate(null, url, out var endpoint, out _).Should().BeTrue();
        var factory = new TestRequestFactory(_ => throw new IOException("connection reset"));
        using var source = new AgentlessConfigurationSource(endpoint!, factory, TimeSpan.FromSeconds(30), _ => true, waitAsync: NoWait, logger: logger);

        await source.PollAsync();

        string.Join("\n", lines).Should().Contain("request timeout=5s")
              .And.Contain("IOException: connection reset")
              .And.Contain("http://localhost").And.Contain("after 3 attempt(s)");
    }

    private static AgentlessConfigurationSource CreateSource(
        TestRequestFactory factory,
        List<ServerConfiguration> applied,
        string? environment = null,
        string? service = null,
        IDatadogLogger? logger = null)
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
            service,
            waitAsync: NoWait,
            logger: logger);

    // A custom base URL needs no API key. A custom endpoint also gets the service, because a service
    // name is not a secret.
    private static TracerSettings CreateTracerSettings(string? service)
    {
        var values = new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSourceAgentlessBaseUrl] = "http://localhost/config",
        };

        if (service is not null)
        {
            values[ConfigurationKeys.ServiceName] = service;
        }

        return new TracerSettings(new NameValueConfigurationSource(values));
    }

    // The tracer builds the source in the same way. Only the transport is different.
    private static AgentlessConfigurationSource CreateConfiguredSource(TracerSettings settings, TestRequestFactory factory, IDatadogLogger? logger = null)
        => AgentlessConfigurationSource.Create(settings.FeatureFlags, settings.Manager, _ => true, logger, factory)
        ?? throw new InvalidOperationException("Create did not produce a source for the test settings.");

    // Changes settings as customer code does, through the manual instrumentation API.
    private static void UpdateSettingsInCode(TracerSettings settings, string key, string value)
    {
        var source = new ManualInstrumentationConfigurationSource(new Dictionary<string, object?> { [key] = value }, useDefaultSources: true);
        settings.Manager.UpdateManualConfigurationSettings(source, NullConfigurationTelemetry.Instance).Should().BeTrue();
    }

    // Returns the services that the source logs at debug level as not sent.
    private static List<object?> RejectedServices(Mock<IDatadogLogger> logger)
        => logger.Invocations
                 .Where(i => i.Method.Name == nameof(IDatadogLogger.Debug) && i.Arguments[0] is string template && template.Contains(ServiceRejection))
                 .Select(i => i.Arguments[1])
                 .ToList<object?>();

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
