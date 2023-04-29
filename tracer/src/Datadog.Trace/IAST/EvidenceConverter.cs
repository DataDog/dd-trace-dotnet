// <copyright file="EvidenceConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Iast;

internal class EvidenceConverter : JsonConverter<Evidence>
{
    private bool _redactionEnabled;

    public EvidenceConverter(bool redactionEnabled)
    {
        _redactionEnabled = redactionEnabled;
    }

    public override bool CanRead => false;

    public override Evidence ReadJson(JsonReader reader, Type objectType, Evidence existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override void WriteJson(JsonWriter writer, Evidence evidence, JsonSerializer serializer)
    {
        if (_redactionEnabled)
        {
            WriteRedactedEvidence(writer, evidence, serializer);
        }
        else
        {
            WriteEvidence(writer, evidence);
        }
    }

    private void WriteEvidence(JsonWriter writer, Evidence evidence)
    {
        writer.WriteStartObject();
        if (evidence.Ranges == null || evidence.Ranges.Length == 0)
        {
            writer.WritePropertyName("value");
            writer.WriteValue(evidence.Value);
        }
        else
        {
            writer.WritePropertyName("valueParts");
            ToJsonTaintedValue(writer, evidence.Value!, evidence.Ranges);
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

            string substring = Substring(value, range);
            WriteValuePart(writer, substring, range);
            start = range.Start + substring.Length;
        }

        if (start < value.Length)
        {
            WriteValuePart(writer, value.Substring(start));
        }

        writer.WriteEndArray();
    }

    private void WriteRedactedEvidence(JsonWriter writer, Evidence evidence, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        if (evidence.Ranges == null && evidence.Sensitive == null)
        {
            writer.WritePropertyName("value");
            writer.WriteValue(evidence.Value);
        }
        else
        {
            writer.WritePropertyName("valueParts");
            ToRedactedJson(writer, evidence.Value!, evidence.Ranges, evidence.Sensitive);
        }

        writer.WriteEndObject();
    }

    private string Substring(string value, Range range)
    {
        if (range.Start >= value.Length) { return string.Empty; }
        else if (range.Start + range.Length >= value.Length) { return value.Substring(range.Start); }
        return value.Substring(range.Start, range.Length);
    }

    private void WriteValuePart(JsonWriter writer, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            WriteValuePart(writer, value, null);
        }
    }

    private void WriteValuePart(JsonWriter writer, string value, Range? range)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("value");
        writer.WriteValue(value);
        if (range != null && range.Value.Source != null)
        {
            writer.WritePropertyName("source");
            writer.WriteValue(range.Value.Source.GetInternalId());
        }

        writer.WriteEndObject();
    }

    private void WriteRedactedValuePart(JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("redacted");
        writer.WriteValue(true);
        writer.WriteEndObject();
    }

    private void WriteRedactedValuePart(JsonWriter writer, Range range)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("redacted");
        writer.WriteValue(true);
        if (range.Source != null)
        {
            writer.WritePropertyName("source");
            writer.WriteValue(range.Source.GetInternalId());
        }

        writer.WriteEndObject();
    }

    private Range? Poll(LinkedList<Range> list)
    {
        if (list.First != null)
        {
            var res = list.First.Value;
            list.RemoveFirst();
            return res;
        }

        return null;
    }

    private void ToRedactedJson(JsonWriter writer, string value, Range[]? taintedRanges, Range[]? sensitiveRanges)
    {
        writer.WriteStartArray();
        int start = 0;
        LinkedList<Range> tainted = taintedRanges != null ? new LinkedList<Range>(taintedRanges) : new LinkedList<Range>();
        LinkedList<Range> sensitive = sensitiveRanges != null ? new LinkedList<Range>(sensitiveRanges) : new LinkedList<Range>();

        Range? nextTainted = Poll(tainted);
        Range? nextSensitive = Poll(sensitive);
        for (int i = 0; i < value.Length; i++)
        {
            if (nextTainted != null && nextTainted.Value.Start == i)
            {
                WriteValuePart(writer, value.Substring(start, i - start));
                // clean up contained sensitive ranges
                while (nextSensitive != null && nextTainted.Value.Contains(nextSensitive.Value))
                {
                    nextTainted.Value.Source?.MarkAsRedacted();
                    nextSensitive = Poll(sensitive);
                }

                if (nextSensitive != null && nextSensitive.Value.Intersects(nextTainted.Value))
                {
                    nextTainted.Value.Source?.MarkAsRedacted();
                    var rest = nextSensitive.Value.Remove(nextTainted.Value);
                    nextSensitive = rest.Count > 0 ? rest[0] : null;
                }

                if (nextTainted.Value.Source != null && nextTainted.Value.Source.IsRedacted())
                {
                    WriteRedactedValuePart(writer, nextTainted.Value);
                }
                else
                {
                    WriteValuePart(writer, Substring(value, nextTainted.Value), nextTainted);
                }

                start = i + nextTainted.Value.Length;
                i = start - 1;
                nextTainted = Poll(tainted);
            }
            else if (nextSensitive != null && nextSensitive.Value.Start == i)
            {
                WriteValuePart(writer, value.Substring(start, i - start));
                if (nextTainted != null && nextSensitive.Value.Intersects(nextTainted.Value))
                {
                    nextTainted.Value.Source?.MarkAsRedacted();
                    foreach (var entry in nextSensitive.Value.Remove(nextTainted.Value))
                    {
                        if (entry.Start == i)
                        {
                            nextSensitive = entry;
                        }
                        else
                        {
                            sensitive.AddFirst(entry);
                        }
                    }
                }

                WriteRedactedValuePart(writer);
                start = i + nextSensitive.Value.Length;
                i = start - 1;
                nextSensitive = Poll(sensitive);
            }
        }

        if (start < value.Length)
        {
            WriteValuePart(writer, value.Substring(start));
        }

        writer.WriteEndArray();
    }
}
