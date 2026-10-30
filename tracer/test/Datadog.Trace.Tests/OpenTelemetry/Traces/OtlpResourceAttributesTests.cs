// <copyright file="OtlpResourceAttributesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.OpenTelemetry.Traces;
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
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SerializeSpans_GlobalResourceAttributesAreNotRepeatedOnSpans(bool useJson, bool otelSemanticsEnabled)
    {
        var settings = new TracerSettings(new DictionaryConfigurationSource(new Dictionary<string, string>
        {
            { "DD_SERVICE", "resource-test" },
            { "OTEL_TRACES_EXPORTER", "otlp" },
            { "DD_TRACE_OTEL_SEMANTICS_ENABLED", otelSemanticsEnabled ? "true" : "false" },
            { "OTEL_RESOURCE_ATTRIBUTES", "team=core,region=us1,workers=7" },
        }));
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

        root.GetTag("team").Should().Be("core");
        child.GetTag("team").Should().Be("core");

        ISpanBufferSerializer serializer = useJson ? new OtlpTracesJsonSerializer() : new OtlpTracesProtobufSerializer();
        var chunk = new TraceChunkModel(new SpanCollection([root, child]));
        var buffer = new byte[64 * 1024];
        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        written.Should().BeGreaterThan(0);
        written += serializer.FinishBody(ref buffer, written, maxSize: buffer.Length);
        var json = useJson
                       ? Encoding.UTF8.GetString(buffer, 0, written)
                       : JsonFormatter.Default.Format(ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, written));
        var request = JObject.Parse(json);
        var resourceSpans = request["resourceSpans"].Should().ContainSingle().Subject;
        var resourceAttributes = resourceSpans["resource"]["attributes"].ToDictionary(a => a["key"].Value<string>(), a => a["value"]);
        var spans = resourceSpans["scopeSpans"].SelectMany(s => s["spans"]).ToDictionary(s => s["name"].Value<string>());
        var rootAttributes = spans["resource-root"]["attributes"].ToDictionary(a => a["key"].Value<string>(), a => a["value"]);
        var childAttributes = spans["resource-child"]["attributes"].ToDictionary(a => a["key"].Value<string>(), a => a["value"]);

        using var assertions = new AssertionScope();
        resourceAttributes.Should().Contain(a => a.Key == "team" && a.Value["stringValue"].Value<string>() == "core");
        resourceAttributes.Should().Contain(a => a.Key == "region" && a.Value["stringValue"].Value<string>() == "us1");
        resourceAttributes.Should().Contain(a => a.Key == "workers" && a.Value["stringValue"].Value<string>() == "7");
        rootAttributes.Should().NotContainKey("team");
        rootAttributes.Should().NotContainKey("region");
        rootAttributes.Should().NotContainKey("workers");
        childAttributes.Should().NotContainKey("team");
        childAttributes.Should().Contain(a => a.Key == "region" && a.Value["stringValue"].Value<string>() == "eu1");
        childAttributes.Should().Contain(a => a.Key == "workers" && a.Value["doubleValue"].Value<double>() == 7);
        childAttributes.Should().Contain(a => a.Key == "span.only" && a.Value["stringValue"].Value<string>() == "retained");
    }
}
