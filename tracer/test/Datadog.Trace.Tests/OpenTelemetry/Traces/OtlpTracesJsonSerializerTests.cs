// <copyright file="OtlpTracesJsonSerializerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using Datadog.Trace.OpenTelemetry.Traces;
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
