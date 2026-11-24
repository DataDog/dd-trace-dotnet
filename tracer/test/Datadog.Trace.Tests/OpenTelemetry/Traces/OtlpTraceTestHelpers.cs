// <copyright file="OtlpTraceTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.OpenTelemetry.Traces;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using OpenTelemetry.Proto.Collector.Trace.V1;

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

internal static class OtlpTraceTestHelpers
{
    internal static Span CreateFinishedSpan(ScopedTracer tracer, string operationName, Action<Span>? configure = null)
    {
        using var scope = tracer.StartActive(operationName);
        var span = (Span)scope.Span;
        span.ResourceName = operationName;
        configure?.Invoke(span);
        return span;
    }

    internal static JObject Serialize(bool useJson, SpanCollection spans)
    {
        ISpanBufferSerializer serializer = useJson ? new OtlpTracesJsonSerializer() : new OtlpTracesProtobufSerializer();
        var chunk = new TraceChunkModel(in spans);
        var buffer = new byte[64 * 1024];
        var written = serializer.SerializeSpans(ref buffer, 0, chunk, spanBufferOffset: 0, maxSize: buffer.Length);
        written.Should().BeGreaterThan(0);
        written += serializer.FinishBody(ref buffer, written, maxSize: buffer.Length);
        return Parse(useJson, buffer, written);
    }

    internal static JObject Parse(bool useJson, byte[] buffer, int length)
    {
        var json = useJson
                       ? Encoding.UTF8.GetString(buffer, 0, length)
                       : JsonFormatter.Default.Format(ExportTraceServiceRequest.Parser.ParseFrom(buffer, 0, length));
        return JObject.Parse(json);
    }

    internal static Dictionary<string, JToken> GetSpans(JToken resourceSpans)
        => resourceSpans["scopeSpans"]!.SelectMany(s => s["spans"]!).ToDictionary(s => s["name"]!.Value<string>()!);

    internal static Dictionary<string, JToken> GetAttributes(JToken token)
        => token["attributes"]?.ToDictionary(a => a["key"]!.Value<string>()!, a => a["value"]!) ?? new Dictionary<string, JToken>();
}
