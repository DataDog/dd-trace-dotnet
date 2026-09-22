// <copyright file="ScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler;

public class ScopeFactoryTests
{
    private static readonly Uri RequestUri = new("http://localhost:8080/api/users?q=1");

    [Fact]
    public async Task CreateOutboundHttpScope_WithDatadogSemantics_UsesDatadogNamesAndValues()
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: false);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", RequestUri, IntegrationId.HttpMessageHandler, out var tags);

        scope.Span.ResourceName.Should().Be("GET localhost:8080/api/users");
        scope.Span.Type.Should().Be(SpanTypes.Http);

        tags.HttpMethod.Should().Be("GET");
        tags.HttpUrl.Should().Be("http://localhost:8080/api/users?q=1");
        tags.Host.Should().Be("localhost");

        // OpenTelemetry-only concepts are not populated
        tags.ServerPort.Should().BeNull();
        tags.HttpRequestMethodOriginal.Should().BeNull();

        var serializedTags = GetSerializedTags(scope.Span);
        serializedTags.Should().Contain(
        [
            new KeyValuePair<string, string>(Tags.HttpMethod, "GET"),
            new KeyValuePair<string, string>(Tags.HttpUrl, "http://localhost:8080/api/users?q=1"),
            new KeyValuePair<string, string>(Tags.OutHost, "localhost"),
        ]);
        serializedTags.Keys.Should().NotContain([Tags.HttpRequestMethod, Tags.UrlFull, Tags.ServerAddress, Tags.ServerPort]);
    }

    [Fact]
    public async Task CreateOutboundHttpScope_WithOpenTelemetrySemantics_UsesOpenTelemetryNamesAndValues()
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: true);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", RequestUri, IntegrationId.HttpMessageHandler, out var tags);

        // there is no low-cardinality target available for HTTP client spans, so the name is just the method
        scope.Span.ResourceName.Should().Be("GET");
        scope.Span.Type.Should().Be(SpanTypes.Http);

        tags.HttpMethod.Should().Be("GET");
        tags.HttpUrl.Should().Be("http://localhost:8080/api/users?q=1");
        tags.Host.Should().Be("localhost");
        tags.ServerPort.Should().Be(8080);
        tags.HttpRequestMethodOriginal.Should().BeNull();

        var serializedTags = GetSerializedTags(scope.Span);
        serializedTags.Should().Contain(
        [
            new KeyValuePair<string, string>(Tags.HttpRequestMethod, "GET"),
            new KeyValuePair<string, string>(Tags.UrlFull, "http://localhost:8080/api/users?q=1"),
            new KeyValuePair<string, string>(Tags.ServerAddress, "localhost"),
            new KeyValuePair<string, string>(Tags.ServerPort, "8080"),
        ]);
        serializedTags.Keys.Should().NotContain([Tags.HttpMethod, Tags.HttpUrl, Tags.OutHost, Tags.HttpRequestMethodOriginal]);
    }

    [Fact]
    public async Task CreateOutboundHttpScope_WithOpenTelemetrySemantics_UsesTheDefaultPortWhenNotSpecified()
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: true);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", new Uri("https://example.com/api"), IntegrationId.HttpMessageHandler, out var tags);

        tags.ServerPort.Should().Be(443);
        tags.HttpUrl.Should().Be("https://example.com/api");
    }

    [Theory]
    // known methods are reported in their canonical form, with the original value when it differs in a case-insensitive comparison
    [InlineData("GET", "GET", null, "GET")]
    [InlineData("get", "GET", null, "GET")]
    [InlineData("Patch", "PATCH", null, "PATCH")]

    // unknown methods are reported as _OTHER, and the span is named HTTP
    [InlineData("FOO", "_OTHER", "FOO", "HTTP")]
    [InlineData(null, "_OTHER", null, "HTTP")]
    public async Task CreateOutboundHttpScope_WithOpenTelemetrySemantics_NormalizesTheRequestMethod(
        string httpMethod, string expectedMethod, string expectedOriginalMethod, string expectedSpanName)
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: true);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, httpMethod, RequestUri, IntegrationId.HttpMessageHandler, out var tags);

        scope.Span.ResourceName.Should().Be(expectedSpanName);
        tags.HttpMethod.Should().Be(expectedMethod);
        tags.HttpRequestMethodOriginal.Should().Be(expectedOriginalMethod);
    }

    [Fact]
    public async Task CreateOutboundHttpScope_WithOpenTelemetrySemantics_RedactsCredentialsInTheUrl()
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: true);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", new Uri("http://user:pass@localhost/api"), IntegrationId.HttpMessageHandler, out var tags);

        tags.HttpUrl.Should().Be("http://REDACTED:REDACTED@localhost/api");
    }

    // DD_TRACE_OTEL_SEMANTICS_ENABLED forces the effective metadata schema version to v0 (see
    // TracerSettings), because OpenTelemetry semantics already fully replace Datadog attribute naming
    // and values, so the V1 schema's Datadog-only attributes (e.g. peer.service) must not coexist with them.
    [Theory]
    [InlineData("v1", true)]
    [InlineData("v1", false)]
    [InlineData("v1", null)]
    [InlineData("v0", true)]
    [InlineData("v0", false)]
    [InlineData("v0", null)]
    [InlineData(null, null)]
    public async Task CreateOutboundHttpScope_WithOpenTelemetrySemantics_NeverUsesV1SchemaTags(string requestedSchemaVersion, bool? peerServiceDefaultsEnabled)
    {
        await using var tracer = CreateTracer(otelSemanticsEnabled: true, schemaVersion: requestedSchemaVersion, peerServiceDefaultsEnabled: peerServiceDefaultsEnabled);
        using var parent = StartParentScope(tracer);

        using var scope = ScopeFactory.CreateOutboundHttpScope(tracer, "GET", RequestUri, IntegrationId.HttpMessageHandler, out var tags);

        tags.Should().BeOfType<HttpTags>();
        tags.GetTag(Tags.PeerService).Should().BeNull();
        tags.GetTag(Tags.PeerServiceSource).Should().BeNull();
    }

    private static Scope StartParentScope(Tracer tracer)
    {
        // Azure Functions support installs a PlatformStrategy.ShouldSkipClientSpan callback that
        // skips parentless client spans. It is a mutable static, so another test in this assembly
        // may already have installed it: always create the client span inside an active scope.
        return tracer.StartActiveInternal("parent");
    }

    private static ScopedTracer CreateTracer(bool otelSemanticsEnabled, string schemaVersion = null, bool? peerServiceDefaultsEnabled = null)
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.OpenTelemetry.OtelSemanticsEnabled, otelSemanticsEnabled ? "true" : "false" },
        };

        if (schemaVersion is not null)
        {
            collection.Add(ConfigurationKeys.MetadataSchemaVersion, schemaVersion);
        }

        if (peerServiceDefaultsEnabled is not null)
        {
            collection.Add(ConfigurationKeys.PeerServiceDefaultsEnabled, peerServiceDefaultsEnabled.Value ? "true" : "false");
        }

        var settings = new TracerSettings(new NameValueConfigurationSource(collection));
        return TracerHelper.CreateWithFakeAgent(settings);
    }

    private static Dictionary<string, string> GetSerializedTags(Span span)
    {
        var result = new Dictionary<string, string>();
        var processor = new TagCollectorProcessor(result);
        span.Tags.EnumerateTags(ref processor, span.OpenTelemetrySemanticsEnabled);
        return result;
    }

    private readonly struct TagCollectorProcessor : IItemProcessor<string>, IItemProcessor<int>
    {
        private readonly Dictionary<string, string> _items;

        public TagCollectorProcessor(Dictionary<string, string> items)
        {
            _items = items;
        }

        public void Process(TagItem<string> item)
        {
            _items[item.Key] = item.Value;
        }

        public void Process(TagItem<int> item)
        {
            _items[item.Key] = item.Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
