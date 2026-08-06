// <copyright file="OtlpTracesJsonSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.OpenTelemetry.Traces;
using Datadog.Trace.Tests.Util;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;
using VendorJsonTextWriter = Datadog.Trace.Vendors.Newtonsoft.Json.JsonTextWriter;

#nullable enable

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

// Targeted regression tests for the OTLP JSON AnyValue array path. Flat-array and
// primitive AnyValue behavior is already exercised by OpenTelemetrySdkTests.SubmitsOtlpTraces
// and by the protobuf serializer's unit tests (which mirror this logic), so we only
// cover the cases the fix specifically changes: nested arrays must stringify, not recurse.
public class OtlpTracesJsonSerializerTests
{
    [Fact]
    public void WriteAnyValue_SelfReferentialObjectArray_IsBoundedAtOneLevel()
    {
        var cycle = new object[1];
        cycle[0] = cycle;

        var json = WriteAnyValue(cycle);

        var values = json["arrayValue"]!["values"]!;
        values.Should().HaveCount(1);
        values[0]!["stringValue"]!.Value<string>().Should().Be(cycle.ToString());
    }

    [Fact]
    public void WriteAnyValue_DeeplyNestedArray_IsBoundedAtOneLevel()
    {
        object[] current = new object[] { "leaf" };
        for (int i = 0; i < 5_000; i++)
        {
            current = new object[] { current };
        }

        var json = WriteAnyValue(current);

        var values = json["arrayValue"]!["values"]!;
        values.Should().HaveCount(1);
        values[0]!["stringValue"]!.Value<string>().Should().Be(typeof(object[]).ToString());
    }

    [Fact]
    public void WriteAnyValue_NestedObjectArray_StringifiesInsteadOfArrayValue()
    {
        var inner = new object[] { "a", 1 };
        var outer = new object[] { inner };

        var json = WriteAnyValue(outer);

        var values = json["arrayValue"]!["values"]!;
        values.Should().HaveCount(1);
        values[0]!["stringValue"]!.Value<string>().Should().Be(inner.ToString());
    }

    [Fact]
    public void WriteAnyValue_Ulong_EmitsStringValue()
    {
        // ulong overflows OTLP intValue (int64), so it must stringify — matches the protobuf
        // serializer and OTel .NET SDK's TagWriter behavior.
        var json = WriteAnyValue(ulong.MaxValue);

        json["stringValue"]!.Value<string>().Should().Be(ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void WriteAnyValue_NestedByteArray_StringifiesInsteadOfBytesValue()
    {
        // Top-level byte[] still emits bytesValue (verified via the integration test).
        // Nested byte[] stringifies — matches OTel .NET SDK's TagWriter.
        var nestedBytes = new byte[] { 0x01, 0x02 };
        var outer = new object[] { nestedBytes };

        var json = WriteAnyValue(outer);

        var values = json["arrayValue"]!["values"]!;
        values.Should().HaveCount(1);
        values[0]!["stringValue"]!.Value<string>().Should().Be(nestedBytes.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteSpan_ErrorSpanWithoutOtelStatusCodeTag_EmitsErrorStatus(bool openTelemetrySemanticsEnabled)
    {
        // Our own instrumentation marks spans as errors via span.Error and never sets "otel.status_code"
        var ddSpan = CreateSpan(openTelemetrySemanticsEnabled: openTelemetrySemanticsEnabled);
        ddSpan.Error = true;
        ddSpan.GetTag("otel.status_code").Should().BeNull();

        var json = WriteSpan(ddSpan);

        var actualStatusCode = json["status"]?["code"]?.Value<int>();
        actualStatusCode.Should().Be((int)StatusCode.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteSpan_NonErrorSpanWithoutOtelStatusCodeTag_OmitsStatus(bool openTelemetrySemanticsEnabled)
    {
        var ddSpan = CreateSpan(openTelemetrySemanticsEnabled: openTelemetrySemanticsEnabled);
        ddSpan.Error.Should().BeFalse();
        ddSpan.GetTag("otel.status_code").Should().BeNull();

        var json = WriteSpan(ddSpan);

        json["status"].Should().BeNull();
    }

    [Fact]
    public void WriteSpan_ErrorSpanWithoutOtelStatusCodeTag_UsesErrorMsgAsStatusMessage()
    {
        var ddSpan = CreateSpan();
        ddSpan.Error = true;
        ddSpan.SetTag(Tags.ErrorMsg, "oops");

        var json = WriteSpan(ddSpan);

        json["status"]!["code"]!.Value<int>().Should().Be((int)StatusCode.Error);
        json["status"]!["message"]!.Value<string>().Should().Be("oops");
    }

    [Theory]
    [InlineData("STATUS_CODE_OK", 1)]
    [InlineData("STATUS_CODE_ERROR", 2)]
    public void WriteSpan_ErrorSpanWithExplicitOtelStatusCodeTag_KeepsTheTagValue(string otelStatusCode, int expectedStatusCode)
    {
        // A status set through the OTel API wins over span.Error
        var ddSpan = CreateSpan(openTelemetrySemanticsEnabled: true);
        ddSpan.Error = true;
        ddSpan.SetTag("otel.status_code", otelStatusCode);

        var json = WriteSpan(ddSpan);

        json["status"]!["code"]!.Value<int>().Should().Be(expectedStatusCode);
    }

    // Under OTel semantics SetHttpStatusCode sets error.type instead of error.msg, and per the
    // HTTP semantic conventions the status description is left unset, so no message is written.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WriteSpan_HttpSpanWithErrorStatusCode_EmitsErrorStatus(bool openTelemetrySemanticsEnabled)
    {
        string? expectedMessage = openTelemetrySemanticsEnabled ? null : "The HTTP response has status code 500.";

        // Regression test for HTTP spans marked as errors by SetHttpStatusCode, which sets
        // span.Error but no "otel.status_code" tag.
        var ddSpan = CreateSpan(openTelemetrySemanticsEnabled: openTelemetrySemanticsEnabled);
        ddSpan.SetHttpStatusCode(500, isServer: true, new TracerSettings().Manager.InitialMutableSettings);
        ddSpan.Error.Should().BeTrue();
        ddSpan.GetTag("otel.status_code").Should().BeNull();

        var json = WriteSpan(ddSpan);

        json["status"]!["code"]!.Value<int>().Should().Be((int)StatusCode.Error);

        // Assign first: "json["status"]!["message"]?.Value<string>().Should()" would short-circuit
        // to a no-op when the message is omitted, making the assertion vacuous.
        var actualMessage = json["status"]!["message"]?.Value<string>();
        actualMessage.Should().Be(expectedMessage);
    }

    private static Span CreateSpan(bool openTelemetrySemanticsEnabled = false)
    {
        var context = new SpanContext(parent: null, new TraceContext(new StubDatadogTracer()), serviceName: "service_name");
        var span = new Span(context, DateTimeOffset.UtcNow, tags: null, links: null, openTelemetrySemanticsEnabled)
        {
            OperationName = "operation_name",
            ResourceName = "resource_name",
        };
        span.SetDuration(TimeSpan.FromMilliseconds(1));
        return span;
    }

    private static JObject WriteSpan(Span span)
    {
        var serializer = new OtlpTracesJsonSerializer();
        var traceChunk = new TraceChunkModel(new SpanCollection(new[] { span }));

        using var stringWriter = new StringWriter();
        using (var writer = new VendorJsonTextWriter(stringWriter))
        {
            serializer.WriteSpan(writer, traceChunk.GetSpanModel(0));
        }

        return JObject.Parse(stringWriter.ToString());
    }

    private static JObject WriteAnyValue(object? value)
    {
        using var stringWriter = new StringWriter();
        using (var writer = new VendorJsonTextWriter(stringWriter))
        {
            OtlpTracesJsonSerializer.WriteAnyValue(writer, value);
        }

        return JObject.Parse(stringWriter.ToString());
    }
}
