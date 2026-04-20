// <copyright file="EvidenceJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Iast.Helpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Iast;

/// <summary>
/// Custom JSON serializer for <see cref="Datadog.Trace.Iast.Evidence"/> struct
/// </summary>
internal sealed class EvidenceJsonConverter : JsonConverter<Evidence?>
{
    // When not redacted output is:
    // "valueParts": [
    //   { "value": "SELECT * FROM Users WHERE " },     -> Not tainted part
    //   { "source": 0, "value": "Name='name'" }        -> Tainted part from source 0
    //   { "value": " and RoleId=25" }                  -> Not tainted part
    // ]
    //
    // When redacted output is:
    // "valueParts": [
    //   { "value": "SELECT * FROM Users WHERE " },            -> Not tainted part
    //   { "source": 0, "value": "Name='" },                   -> Tainted part from source 0
    //   { "source": 0, "redacted": true, "pattern": "abcd" }, -> Redacted tainted part from source 0
    //   { "source": 0, "value": "'" },                        -> Tainted part from source 0
    //   { "value": " and RoleId=" },                          -> Not tainted part
    //   { "redacted": true }                                  -> Redacted not tainted part
    // ]

    // Explanation:
    //   Input is a string value, tainted ranges and sensitive ranges.
    //   We must produce an array of "value parts", with are segments of the input value.
    //   Non-sensitive parts are plain text. Sensitive ranges must be redacted.
    //   If a tainted range intersects with a sensitive range, the corresponding source must be redacted also.

    private bool _redactionEnabled;
    private int _maxValueLength;

    public EvidenceJsonConverter(int maxValueLength, bool redactionEnabled)
    {
        _redactionEnabled = redactionEnabled;
        _maxValueLength = maxValueLength;
    }

    public override bool CanRead => false;

    public override Evidence? ReadJson(JsonReader reader, Type objectType, Evidence? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        return null;
    }

    public override void WriteJson(JsonWriter writer, Evidence? evidence, JsonSerializer serializer)
    {
        if (evidence is null)
        {
            writer.WriteNull();
            return;
        }

        var evidenceValue = evidence.Value;

        writer.WriteStartObject();
        if (evidenceValue.Ranges == null || evidenceValue.Ranges.Length == 0)
        {
            writer.WritePropertyName("value");
            writer.WriteTruncatableValue(evidenceValue.Value, _maxValueLength);
        }
        else
        {
            writer.WritePropertyName("valueParts");
            if (_redactionEnabled)
            {
                ToRedactedJson(writer, evidenceValue.Value!, evidenceValue.Ranges, evidenceValue.Sensitive);
            }
            else
            {
                ToJsonTaintedValue(writer, evidenceValue.Value!, evidenceValue.Ranges);
            }
        }

        writer.WriteEndObject();
    }

    private void ToJsonTaintedValue(JsonWriter writer, string value, Range[] ranges)
    {
        writer.WriteStartArray();
        int start = 0;
        foreach (var range in ranges)
        {
            if (range.Start > start)
            {
                WriteValuePart(writer, value.Substring(start, range.Start - start));
            }

            string substring = EvidenceConverterHelper.Substring(value, range);
            WriteValuePart(writer, substring, range.Source);
            start = range.Start + substring.Length;
        }

        if (start < value.Length)
        {
            WriteValuePart(writer, value.Substring(start));
        }

        writer.WriteEndArray();
    }

    private void WriteValuePart(JsonWriter writer, string? value, Source? source = null)
    {
        if (value == null) { return; }
        writer.WriteStartObject();
        writer.WritePropertyName("value");
        writer.WriteTruncatableValue(value, _maxValueLength);
        if (source != null)
        {
            writer.WritePropertyName("source");
            writer.WriteValue(source.GetInternalId());
        }

        writer.WriteEndObject();
    }

    private void WriteRedactedValuePart(JsonWriter writer, string? value, Source? source = null)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("redacted");
        writer.WriteValue(true);
        if (value != null)
        {
            writer.WritePropertyName("pattern");
            writer.WriteTruncatableValue(value, _maxValueLength);
        }

        if (source != null)
        {
            writer.WritePropertyName("source");
            writer.WriteValue(source.GetInternalId());
        }

        writer.WriteEndObject();
    }

    private void Write(JsonWriter writer, EvidenceConverterHelper.ValuePart part)
    {
        if (part.IsRedacted)
        {
            WriteRedactedValuePart(writer, part.Value, part.Source);
        }
        else if (!part.ShouldRedact)
        {
            WriteValuePart(writer, part.Value, part.Source);
        }
        else
        {
            foreach (var valuePart in part.Split())
            {
                Write(writer, valuePart);
            }
        }
    }

    private void ToRedactedJson(JsonWriter writer, string value, Range[]? taintedRanges, Range[]? sensitiveRanges)
    {
        writer.WriteStartArray();
        LinkedList<Range> tainted = taintedRanges != null ? new LinkedList<Range>(taintedRanges) : new LinkedList<Range>();
        LinkedList<Range> sensitive = sensitiveRanges != null ? new LinkedList<Range>(sensitiveRanges) : new LinkedList<Range>();

        var parts = new EvidenceConverterHelper.ValuePartIterator(value, tainted, sensitive);
        foreach (var part in parts)
        {
            if (part != null)
            {
                Write(writer, part.Value);
            }
        }

        writer.WriteEndArray();
    }
}
