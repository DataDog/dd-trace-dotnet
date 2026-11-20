// <copyright file="OtlpResourceAttributesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.OpenTelemetry.Traces;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using FluentAssertions.Execution;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Trace.V1;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

public class OtlpResourceAttributesTests
{
    [Theory]
    [CombinatorialData]
    public async Task SerializeSpans_GlobalResourceAttributesAreNotRepeatedOnSpans(bool useJson, bool otelSemanticsEnabled)
    {
        var settings = CreateSettings(otelResourceAttributes: "team=core,region=us1,workers=7", otelSemanticsEnabled: otelSemanticsEnabled);
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        using var rootScope = tracer.StartActive("resource-root");
        using var childScope = tracer.StartActive("resource-child");
        var root = (Span)rootScope.Span;
        var child = (Span)childScope.Span;
        child.SetTag("region", "eu1");
        child.SetTag("workers", null);
        child.SetMetric("workers", 7);
        child.SetTag("span.only", "retained");
        root.Finish();
        child.Finish();

        // global tags are still set on the spans in memory, only the OTLP encoding changes
        root.GetTag("team").Should().Be("core");
        child.GetTag("team").Should().Be("core");

        var request = Serialize(useJson, new SpanCollection([root, child]));
        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        var resourceAttributes = GetResourceAttributes(resourceSpans);
        var spans = GetSpans(resourceSpans);
        var rootAttributes = GetAttributes(spans["resource-root"]);
        var childAttributes = GetAttributes(spans["resource-child"]);

        using var assertions = new AssertionScope();
        resourceAttributes.Should().Contain(a => a.Key == "team" && a.Value["stringValue"]!.Value<string>() == "core");
        resourceAttributes.Should().Contain(a => a.Key == "region" && a.Value["stringValue"]!.Value<string>() == "us1");
        resourceAttributes.Should().Contain(a => a.Key == "workers" && a.Value["stringValue"]!.Value<string>() == "7");
        rootAttributes.Should().NotContainKey("team");
        rootAttributes.Should().NotContainKey("region");
        rootAttributes.Should().NotContainKey("workers");
        childAttributes.Should().NotContainKey("team");
        childAttributes.Should().Contain(a => a.Key == "region" && a.Value["stringValue"]!.Value<string>() == "eu1");
        childAttributes.Should().Contain(a => a.Key == "workers" && a.Value["doubleValue"]!.Value<double>() == 7);
        childAttributes.Should().Contain(a => a.Key == "span.only" && a.Value["stringValue"]!.Value<string>() == "retained");
    }

    [Theory]
    [CombinatorialData]
    public async Task SerializeSpans_TracerResourceAttributesAndSpanLevelKeysAreNotTakenFromGlobalTags(bool useJson)
    {
        var settings = CreateSettings(ddTags: "team:core,telemetry.sdk.name:custom,runtime-id:custom,_dd.custom:value,aas.site.name:custom,peer.service:custom");
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        var span = CreateFinishedSpan(tracer, "span");

        var request = Serialize(useJson, new SpanCollection([span]));
        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        var resourceAttributes = GetResourceAttributes(resourceSpans);

        // A top-level span already carries "runtime-id" twice (the tracer's own value plus the DD_TAGS one, which stays
        // on the span), so span attributes are read as a lookup instead of a dictionary.
        var spanAttributes = GetSpans(resourceSpans)["span"]["attributes"]!.ToLookup(a => a["key"]!.Value<string>()!, a => a["value"]!);

        using var assertions = new AssertionScope();
        resourceAttributes["team"]["stringValue"]!.Value<string>().Should().Be("core");
        resourceAttributes["telemetry.sdk.name"]["stringValue"]!.Value<string>().Should().Be(TracerConstants.TelemetrySdkName);
        resourceAttributes[Tags.RuntimeId]["stringValue"]!.Value<string>().Should().Be(Tracer.RuntimeId);
        resourceAttributes.Should().NotContainKeys("_dd.custom", "aas.site.name", "peer.service");

        spanAttributes.Contains("team").Should().BeFalse();
        spanAttributes["_dd.custom"].Should().ContainSingle().Which["stringValue"]!.Value<string>().Should().Be("value");
        spanAttributes["peer.service"].Should().ContainSingle().Which["stringValue"]!.Value<string>().Should().Be("custom");
    }

    [Theory]
    [CombinatorialData]
    public async Task Payload_ChunksWithDifferentGlobalTags_AreWrittenUnderTheirOwnResource(bool useJson)
    {
        // Two settings snapshots, e.g. before and after remote configuration changed "tracing_tags"
        await using var tracerUs = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "team:core,region:us1"));
        await using var tracerEu = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "team:core,region:eu1"));

        var request = SerializePayload(
            useJson,
            new SpanCollection([CreateFinishedSpan(tracerUs, "us-1")]),
            new SpanCollection([CreateFinishedSpan(tracerUs, "us-2")]),
            new SpanCollection([CreateFinishedSpan(tracerEu, "eu-1")]),
            new SpanCollection([CreateFinishedSpan(tracerUs, "us-3")]));

        var resourceSpans = request["resourceSpans"]!.ToList();

        using var assertions = new AssertionScope();
        resourceSpans.Should().HaveCount(3);
        resourceSpans.Select(r => GetResourceAttributes(r)["region"]["stringValue"]!.Value<string>())
                     .Should().Equal("us1", "eu1", "us1");
        resourceSpans.Select(r => string.Join(",", GetSpans(r).Keys))
                     .Should().Equal("us-1,us-2", "eu-1", "us-3");

        foreach (var span in resourceSpans.SelectMany(r => GetSpans(r).Values))
        {
            GetAttributes(span).Should().NotContainKeys("team", "region");
        }
    }

    [Theory]
    [CombinatorialData]
    public async Task Payload_ChunksWithEquivalentGlobalTags_ShareOneResource(bool useJson)
    {
        // Different settings instances with the same global tags
        await using var tracer1 = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "team:core,region:us1"));
        await using var tracer2 = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "region:us1,team:core"));

        var request = SerializePayload(
            useJson,
            new SpanCollection([CreateFinishedSpan(tracer1, "span-1")]),
            new SpanCollection([CreateFinishedSpan(tracer2, "span-2")]));

        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        GetSpans(resourceSpans).Keys.Should().BeEquivalentTo("span-1", "span-2");
    }

    [Theory]
    [CombinatorialData]
    public async Task Payload_ChunksWithDifferentEnvironmentOrVersion_AreWrittenUnderTheirOwnResource(bool useJson, bool changeVersion)
    {
        // Span.SetTag("env"/"version") updates the trace's TraceContext, and env/version are only written on the
        // resource (never on spans), so a chunk whose trace has a different env/version needs its own resource.
        await using var tracer = TracerHelper.CreateWithFakeAgent(CreateSettings(environment: "prod", serviceVersion: "1.0.0"));
        var tagKey = changeVersion ? "version" : "env";
        var resourceKey = changeVersion ? "service.version" : "deployment.environment.name";
        var defaultValue = changeVersion ? "1.0.0" : "prod";

        var first = CreateFinishedSpan(tracer, "default-1");

        var changed = CreateFinishedSpan(tracer, "changed", span => span.SetTag(tagKey, "other"));

        var request = SerializePayload(
            useJson,
            new SpanCollection([first]),
            new SpanCollection([changed]),
            new SpanCollection([CreateFinishedSpan(tracer, "default-2")]));

        var resourceSpans = request["resourceSpans"]!.ToList();

        using var assertions = new AssertionScope();
        resourceSpans.Should().HaveCount(3);
        resourceSpans.Select(r => GetResourceAttributes(r)[resourceKey]["stringValue"]!.Value<string>())
                     .Should().Equal(defaultValue, "other", defaultValue);
        resourceSpans.Select(r => string.Join(",", GetSpans(r).Keys))
                     .Should().Equal("default-1", "changed", "default-2");
    }

    [Theory]
    [CombinatorialData]
    public async Task SerializeSpans_ChunkThatWasNotCommitted_DoesNotOpenAResource(bool useJson)
    {
        await using var tracerUs = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "region:us1"));
        await using var tracerEu = TracerHelper.CreateWithFakeAgent(CreateSettings(ddTags: "region:eu1"));

        ISpanBufferSerializer serializer = useJson ? new OtlpTracesJsonSerializer() : new OtlpTracesProtobufSerializer();
        var payload = new byte[64 * 1024];
        var temporaryBuffer = new byte[64 * 1024];
        var offset = serializer.HeaderSize;

        // first chunk is committed into the payload
        offset += Append(serializer, ref temporaryBuffer, payload, offset, new SpanCollection([CreateFinishedSpan(tracerUs, "us-1")]));

        // second chunk is serialized, but the caller rejects it (e.g. SpanBuffer couldn't grow its buffer)
        var rejected = serializer.SerializeSpans(ref temporaryBuffer, 0, new TraceChunkModel(new SpanCollection([CreateFinishedSpan(tracerEu, "eu-1")])), offset, maxSize: payload.Length);
        rejected.Should().BeGreaterThan(0);

        // third chunk is committed after the rejected one, and shares the first chunk's resource
        offset += Append(serializer, ref temporaryBuffer, payload, offset, new SpanCollection([CreateFinishedSpan(tracerUs, "us-2")]));
        offset += serializer.FinishBody(ref payload, offset, payload.Length);

        var request = Parse(useJson, payload, offset);
        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        GetResourceAttributes(resourceSpans)["region"]["stringValue"]!.Value<string>().Should().Be("us1");
        GetSpans(resourceSpans).Keys.Should().Equal("us-1", "us-2");
    }

    [Fact]
    public void Get_CachesPerGlobalTagsInstanceAndFiltersAndSorts()
    {
        var globalTags = CreateGlobalTags("team", "core", "_dd.internal", "x", "region", "us1");

        var resourceGlobalTags = OtlpResourceGlobalTags.Get(globalTags);

        using var assertions = new AssertionScope();
        resourceGlobalTags.Should().NotBeNull();
        OtlpResourceGlobalTags.Get(globalTags).Should().BeSameAs(resourceGlobalTags);
        resourceGlobalTags!.Attributes.Select(a => a.Key).Should().Equal("region", "team");
        resourceGlobalTags.Contains("team", "core").Should().BeTrue();
        resourceGlobalTags.Contains("team", "other").Should().BeFalse();
        resourceGlobalTags.Contains("_dd.internal", "x").Should().BeFalse();
        resourceGlobalTags.Contains(null, "core").Should().BeFalse();
        OtlpResourceGlobalTags.Get(null).Should().BeNull();
        OtlpResourceGlobalTags.Get(CreateGlobalTags()).Should().BeNull();
        OtlpResourceGlobalTags.Get(CreateGlobalTags("_dd.internal", "x")).Should().BeNull();
    }

    [Fact]
    public void AreEquivalent_ComparesResourceGlobalTagsByContent()
    {
        var resourceGlobalTags = OtlpResourceGlobalTags.Get(CreateGlobalTags("team", "core", "region", "us1"));

        using var assertions = new AssertionScope();
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, resourceGlobalTags).Should().BeTrue();
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, OtlpResourceGlobalTags.Get(CreateGlobalTags("region", "us1", "team", "core"))).Should().BeTrue();
        // keys that are not written to the resource don't make resources different
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, OtlpResourceGlobalTags.Get(CreateGlobalTags("team", "core", "region", "us1", "_dd.internal", "x"))).Should().BeTrue();
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, OtlpResourceGlobalTags.Get(CreateGlobalTags("team", "core", "region", "eu1"))).Should().BeFalse();
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, OtlpResourceGlobalTags.Get(CreateGlobalTags("team", "core"))).Should().BeFalse();
        OtlpResourceGlobalTags.AreEquivalent(resourceGlobalTags, null).Should().BeFalse();
        OtlpResourceGlobalTags.AreEquivalent(null, null).Should().BeTrue();
    }

    [Theory]
    [InlineData("team", true)]
    [InlineData("service.instance.id", true)]
    [InlineData("service", false)]
    [InlineData("deployment.environment.name", false)]
    [InlineData("Telemetry.Sdk.Language", false)]
    [InlineData("runtime-id", false)]
    [InlineData("git.commit.sha", false)]
    [InlineData("datadog.sdk.semantics", false)]
    [InlineData("_dd.origin", false)]
    [InlineData("_dd1.sr.eausr", false)]
    [InlineData("aas.site.name", false)]
    [InlineData("sampling.priority", false)]
    [InlineData("manual.keep", false)]
    [InlineData("peer.service", false)]
    [InlineData("span.kind", false)]
    [InlineData("error.msg", false)]
    [InlineData("http.method", false)]
    [InlineData("http.request.method", false)]
    [InlineData("http.status_code", false)]
    [InlineData("server.port", false)]
    [InlineData("", false)]
    public void IsResourceAttribute(string key, bool expected)
    {
        OtlpResourceGlobalTags.IsResourceAttribute(key).Should().Be(expected);
    }

    [Theory]
    [CombinatorialData]
    public async Task SerializeSpans_GlobalTagBackedByTypedTagProperty_StaysOnTheSpanOnly(bool useJson, bool otelSemanticsEnabled)
    {
        // "http.method" is stored in HttpTags.HttpMethod, which a span exports as "http.request.method"
        // under OpenTelemetry semantics, so it can't be matched against a resource attribute.
        var settings = CreateSettings(ddTags: "team:core,http.method:GET", otelSemanticsEnabled: otelSemanticsEnabled);
        await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
        var span = tracer.StartSpan("typed", tags: new HttpTags());
        span.ResourceName = "typed";
        span.Finish();

        var request = Serialize(useJson, new SpanCollection([span]));
        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        var resourceAttributes = GetResourceAttributes(resourceSpans);
        var spanAttributes = GetAttributes(GetSpans(resourceSpans)["typed"]);
        var expectedSpanKey = otelSemanticsEnabled ? "http.request.method" : "http.method";

        using var assertions = new AssertionScope();
        resourceAttributes["team"]["stringValue"]!.Value<string>().Should().Be("core");
        resourceAttributes.Should().NotContainKeys("http.method", "http.request.method");
        spanAttributes.Should().NotContainKey("team");
        spanAttributes[expectedSpanKey]["stringValue"]!.Value<string>().Should().Be("GET");
    }

    [Fact]
    public void TypedTagKeys_AreNotWrittenToTheResource()
    {
        // Every [Tag] property that a span doesn't export as the same string attribute must be in
        // OtlpResourceGlobalTags.TypedTagKeys: properties with an OtelName (both names), and non-string properties.
        Type[] types;
        try
        {
            types = typeof(Tracer).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }

        var typedTagKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types.Where(t => typeof(ITags).IsAssignableFrom(t)))
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var tagAttribute = property.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "TagAttribute");
                if (tagAttribute is null)
                {
                    continue;
                }

                var tagName = (string)tagAttribute.GetType().GetProperty("TagName")!.GetValue(tagAttribute)!;
                var otelName = (string?)tagAttribute.GetType().GetProperty("OtelName")!.GetValue(tagAttribute);
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                if (otelName is not null)
                {
                    typedTagKeys.Add(tagName);
                    typedTagKeys.Add(otelName);
                }
                else if (propertyType != typeof(string))
                {
                    typedTagKeys.Add(tagName);
                }
            }
        }

        typedTagKeys.Should().Contain(["http.method", "http.request.method", "http.status_code", "server.port"]);
        // any key listed here must be added to OtlpResourceGlobalTags.TypedTagKeys
        typedTagKeys.Where(key => OtlpResourceGlobalTags.IsResourceAttribute(key)).Should().BeEmpty();
    }

    private static ReadOnlyDictionary<string, string> CreateGlobalTags(params string[] keyValues)
    {
        var tags = new Dictionary<string, string>();
        for (var i = 0; i < keyValues.Length; i += 2)
        {
            tags[keyValues[i]] = keyValues[i + 1];
        }

        return new ReadOnlyDictionary<string, string>(tags);
    }

    private static TracerSettings CreateSettings(string? ddTags = null, string? otelResourceAttributes = null, bool otelSemanticsEnabled = false, string? environment = null, string? serviceVersion = null)
    {
        var config = new Dictionary<string, string>
        {
            { "DD_SERVICE", "resource-test" },
            { "OTEL_TRACES_EXPORTER", "otlp" },
            { "DD_TRACE_OTEL_SEMANTICS_ENABLED", otelSemanticsEnabled ? "true" : "false" },
        };

        if (ddTags is not null)
        {
            config["DD_TAGS"] = ddTags;
        }

        if (otelResourceAttributes is not null)
        {
            config["OTEL_RESOURCE_ATTRIBUTES"] = otelResourceAttributes;
        }

        if (environment is not null)
        {
            config["DD_ENV"] = environment;
        }

        if (serviceVersion is not null)
        {
            config["DD_VERSION"] = serviceVersion;
        }

        return new TracerSettings(new DictionaryConfigurationSource(config));
    }

    private static Span CreateFinishedSpan(ScopedTracer tracer, string operationName, Action<Span>? configure = null)
    {
        using var scope = tracer.StartActive(operationName);
        var span = (Span)scope.Span;
        span.ResourceName = operationName;
        configure?.Invoke(span);
        return span;
    }

    private static JObject Serialize(bool useJson, SpanCollection spans)
    {
        ISpanBufferSerializer serializer = useJson ? new OtlpTracesJsonSerializer() : new OtlpTracesProtobufSerializer();
        var chunk = new TraceChunkModel(in spans);
        var buffer = new byte[64 * 1024];
        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        written.Should().BeGreaterThan(0);
        written += serializer.FinishBody(ref buffer, written, maxSize: buffer.Length);
        return Parse(useJson, buffer, written);
    }

    private static JObject SerializePayload(bool useJson, params SpanCollection[] chunks)
    {
        ISpanBufferSerializer serializer = useJson ? new OtlpTracesJsonSerializer() : new OtlpTracesProtobufSerializer();
        var spanBuffer = new SpanBuffer(64 * 1024, serializer);
        var temporaryBuffer = new byte[64 * 1024];

        foreach (var chunk in chunks)
        {
            spanBuffer.TryWrite(in chunk, ref temporaryBuffer).Should().Be(SpanBuffer.WriteStatus.Success);
        }

        var payload = spanBuffer.Detach(new byte[64 * 1024]);
        return Parse(useJson, payload.Data.Array!, payload.Data.Count);
    }

    private static int Append(ISpanBufferSerializer serializer, ref byte[] temporaryBuffer, byte[] payload, int offset, SpanCollection spans)
    {
        var size = serializer.SerializeSpans(ref temporaryBuffer, 0, new TraceChunkModel(in spans), offset, maxSize: payload.Length);
        size.Should().BeGreaterThan(0);
        Buffer.BlockCopy(temporaryBuffer, 0, payload, offset, size);
        return size;
    }

    private static JObject Parse(bool useJson, byte[] buffer, int length)
    {
        var json = useJson
                       ? Encoding.UTF8.GetString(buffer, 0, length)
                       : JsonFormatter.Default.Format(ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, length));
        return JObject.Parse(json);
    }

    private static Dictionary<string, JToken> GetResourceAttributes(JToken resourceSpans)
        => GetAttributes(resourceSpans["resource"]!);

    private static Dictionary<string, JToken> GetSpans(JToken resourceSpans)
        => resourceSpans["scopeSpans"]!.SelectMany(s => s["spans"]!).ToDictionary(s => s["name"]!.Value<string>()!);

    private static Dictionary<string, JToken> GetAttributes(JToken token)
        => token["attributes"]?.ToDictionary(a => a["key"]!.Value<string>()!, a => a["value"]!) ?? new Dictionary<string, JToken>();
}
