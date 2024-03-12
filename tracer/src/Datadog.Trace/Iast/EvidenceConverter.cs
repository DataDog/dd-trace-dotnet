// <copyright file="EvidenceConverter.cs" company="Datadog">
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
internal class EvidenceConverter : JsonConverter<Evidence?>
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
    //   Non sensitive parts are plain text. Sensitive ranges must be redacted.
    //   If a tainted range intersects with a sensitive range, the corresponding source must be redacted also.

    private bool _redactionEnabled;
    private int _maxValueLength;

    public EvidenceConverter(int maxValueLength, bool redactionEnabled)
    {
        _redactionEnabled = redactionEnabled;
        _maxValueLength = maxValueLength;
    }

    public override bool CanRead => false;

    private static string Substring(string value, Range range)
    {
        if (range.Start >= value.Length) { return string.Empty; }
        else if (range.Start + range.Length >= value.Length) { return value.Substring(range.Start); }
        return value.Substring(range.Start, range.Length);
    }

    public override Evidence? ReadJson(JsonReader reader, Type objectType, Evidence? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
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

            string substring = Substring(value, range);
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

    private void Write(JsonWriter writer, ValuePart part)
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

        var parts = new ValuePartIterator(value, tainted, sensitive);
        foreach (var part in parts)
        {
            if (part != null)
            {
                Write(writer, part.Value);
            }
        }

        writer.WriteEndArray();
    }

    private readonly struct ValuePart
    {
        public ValuePart(string value) // String value part
        {
            this.Value = value;
            this.Source = null;
            this.SensitiveRanges = null;
            this.IsRedacted = false;
        }

        public ValuePart(Source? source) // Redacted value part
        {
            this.Value = null;
            this.Source = source;
            this.SensitiveRanges = null;
            this.IsRedacted = true;
        }

        public ValuePart(string value, Source? source, bool isRedacted = false) // Non redacted value part
        {
            this.Value = value;
            this.Source = source;
            this.SensitiveRanges = null;
            this.IsRedacted = isRedacted;
        }

        public ValuePart(string value, Range range, LinkedList<Range> intersections) // Redactable value part
        {
            this.Value = value;
            this.Source = range.Source;

            // shift ranges to the start of the tainted range and sort them
            this.SensitiveRanges = new LinkedList<Range>(intersections.Select(r => r.Shift(-range.Start)).OrderBy(r => r.Start));
            this.IsRedacted = false;
        }

        public string? Value { get; }

        public Source? Source { get; }

        public bool IsRedacted { get; }

        public LinkedList<Range>? SensitiveRanges { get; }

        public bool ShouldRedact => !IsRedacted && (SensitiveRanges != null || (Source != null && Source.IsSensitive));

        public List<ValuePart> Split()
        {
            var parts = new List<ValuePart>();
            if (Value != null)
            {
                if (Source != null && Source.IsSensitive)
                {
                    // redact the full tainted value as the source is sensitive (password, certificate, ...)
                    AddValuePart(0, Value.Length, true, parts);
                }
                else if (SensitiveRanges != null)
                {
                    // redact only sensitive parts
                    int index = 0;
                    foreach (var sensitive in SensitiveRanges)
                    {
                        var start = sensitive.Start;
                        var end = sensitive.Start + sensitive.Length;
                        // append previous tainted chunk (if any)
                        AddValuePart(index, start, false, parts);
                        // append current sensitive tainted chunk
                        AddValuePart(start, end, true, parts);
                        index = end;
                    }

                    // append last tainted chunk (if any)
                    AddValuePart(index, Value.Length, false, parts);
                }
            }

            return parts;
        }

        private void AddValuePart(int start, int end, bool redact, List<ValuePart> valueParts)
        {
            if (start < end && Value != null)
            {
                var chunk = Value.Substring(start, end - start);
                if (!redact)
                {
                    // append the value
                    valueParts.Add(new ValuePart(chunk, Source));
                }
                else if (Source?.Value != null)
                {
                    var length = chunk.Length;
                    var matching = Source.Value.IndexOf(chunk);
                    if (matching >= 0 && Source.RedactedValue != null)
                    {
                        // if matches append the matching part from the redacted value
                        var pattern = Source.RedactedValue.Substring(matching, length);
                        valueParts.Add(new ValuePart(pattern, Source, true));
                    }
                    else
                    {
                        // otherwise redact the string
                        var pattern = Source.RedactString(chunk);
                        valueParts.Add(new ValuePart(pattern, Source, true));
                    }
                }
                else
                {
                    // Null source
                    var pattern = Source.RedactString(chunk);
                    valueParts.Add(new ValuePart(pattern, Source, true));
                }
            }
        }
    }

    private class ValuePartIterator : IEnumerable<ValuePart?>
    {
        // private Context ctx;
        private string value;
        private LinkedList<Range> tainted;
        private LinkedList<Range> sensitive;
        private Dictionary<Range, LinkedList<Range>> intersections = new Dictionary<Range, LinkedList<Range>>();
        private LinkedList<ValuePart> next = new LinkedList<ValuePart>();

        public ValuePartIterator(string value, LinkedList<Range> tainted, LinkedList<Range> sensitive)
        {
            this.value = value;
            this.tainted = tainted;
            this.sensitive = sensitive;
        }

        IEnumerator<ValuePart?> IEnumerable<ValuePart?>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        private class Enumerator : IEnumerator<ValuePart?>
        {
            private ValuePartIterator iterator;

            private int index = 0;
            private ValuePart? current = null;

            public Enumerator(ValuePartIterator iterator)
            {
                this.iterator = iterator;
            }

            object? IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            public ValuePart? Current
            {
                get
                {
                    return current;
                }
            }

            public bool MoveNext()
            {
                if (!HasNext())
                {
                    return false;
                }

                if (!iterator.next.IsEmpty())
                {
                    current = iterator.next.Poll();
                    return true;
                }

                if (iterator.tainted.IsEmpty() && iterator.sensitive.IsEmpty())
                {
                    current = NextStringValuePart(iterator.value.Length); // last string chunk
                    return true;
                }

                var nextTainted = iterator.tainted.Poll();
                var nextSensitive = iterator.sensitive.Poll();
                if (nextTainted != null)
                {
                    if (nextTainted.Value.IsBefore(nextSensitive))
                    {
                        AddNextStringValuePart(nextTainted.Value.Start, iterator.next); // pending string chunk
                        nextSensitive = HandleTaintedValue(nextTainted.Value, nextSensitive);
                    }
                    else
                    {
                        iterator.tainted.AddFirst(nextTainted.Value);
                    }
                }

                if (nextSensitive != null)
                {
                    if (nextSensitive.Value.IsBefore(nextTainted))
                    {
                        AddNextStringValuePart(nextSensitive.Value.Start, iterator.next); // pending string chunk
                        HandleSensitiveValue(nextSensitive);
                    }
                    else
                    {
                        iterator.sensitive.AddFirst(nextSensitive.Value);
                    }
                }

                current = iterator.next.Poll();
                return true;
            }

            public void Dispose()
            {
            }

            public bool HasNext()
            {
                return !iterator.next.IsEmpty() || index < iterator.value.Length;
            }

            public void Reset()
            {
            }

            private Range? HandleTaintedValue(Range nextTainted, Range? nextSensitive)
            {
                var intersections = iterator.intersections.GetAndRemove(nextTainted);
                intersections = intersections ?? new LinkedList<Range>();

                // remove fully overlapped sensitive ranges
                while (nextSensitive != null && nextTainted.Contains(nextSensitive.Value))
                {
                    nextTainted.Source?.MarkAsRedacted();
                    intersections.AddLast(nextSensitive.Value);
                    nextSensitive = iterator.sensitive.Poll();
                }

                Range? intersection = null;
                // truncate last sensitive range if intersects with the tainted one
                if (nextSensitive != null && (intersection = nextTainted.Intersection(nextSensitive.Value)) != null)
                {
                    nextTainted.Source?.MarkAsRedacted();
                    intersections.AddLast(intersection.Value);
                    nextSensitive = RemoveTaintedRange(nextSensitive.Value, nextTainted);
                }

                // finally add value part
                string taintedValue = Substring(iterator.value, nextTainted);
                iterator.next.AddLast(new ValuePart(taintedValue, nextTainted, intersections));
                index = nextTainted.Start + nextTainted.Length;
                return nextSensitive;
            }

            private void HandleSensitiveValue(Range? nextSensitive)
            {
                // truncate sensitive part if intersects with the next tainted range
                Range? nextTainted = iterator.tainted.Peek();
                Range? intersection = null;
                if (nextTainted != null && nextSensitive != null && (intersection = nextTainted.Value.Intersection(nextSensitive)) != null)
                {
                    nextTainted!.Value.Source?.MarkAsRedacted();
                    iterator.intersections.Get(nextTainted.Value, r => new LinkedList<Range>()).AddLast(intersection.Value);
                    nextSensitive = RemoveTaintedRange(nextSensitive.Value, nextTainted.Value);
                }

                // finally add value part
                if (nextSensitive != null)
                {
                    // string sensitiveValue = Substring(iterator.value, nextSensitive.Value);
                    // iterator.next.AddLast(new RedactedValuePart(sensitiveValue));
                    iterator.next.AddLast(new ValuePart(iterator.next.Peek()?.Source));
                    index = nextSensitive.Value.Start + nextSensitive.Value.Length;
                }
            }

            // Removes the tainted range from the sensitive one and returns whatever is before and enqueues the rest
            private Range? RemoveTaintedRange(Range sensitive, Range tainted)
            {
                List<Range> disjointRanges = sensitive.Remove(tainted);
                Range? result = null;
                foreach (var disjoint in disjointRanges)
                {
                    if (disjoint.IsBefore(tainted))
                    {
                        result = disjoint;
                    }
                    else
                    {
                        iterator.sensitive.AddFirst(disjoint);
                    }
                }

                return result;
            }

            private ValuePart? NextStringValuePart(int end)
            {
                if (index < end)
                {
                    string chunk = iterator.value.Substring(index, end - index);
                    index = end;
                    return new ValuePart(chunk);
                }

                return null;
            }

            private void AddNextStringValuePart(int end, LinkedList<ValuePart>? target)
            {
                var part = NextStringValuePart(end);
                if (part != null && target != null)
                {
                    target.AddLast(part.Value);
                }
            }
        }
    }
}
