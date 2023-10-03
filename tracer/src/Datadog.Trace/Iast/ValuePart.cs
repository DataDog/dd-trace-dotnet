// <copyright file="ValuePart.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Iast;

internal readonly struct ValuePart
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
        if (start < end)
        {
            var chunk = Value!.Substring(start, end - start);
            if (!redact)
            {
                // append the value
                valueParts.Add(new ValuePart(chunk, Source));
            }
            else
            {
                var length = chunk.Length;
                var matching = Source!.Value!.IndexOf(chunk);
                if (matching >= 0)
                {
                    // if matches append the matching part from the redacted value
                    var pattern = Source!.RedactedValue!.Substring(matching, length);
                    valueParts.Add(new ValuePart(pattern, Source, true));
                }
                else
                {
                    // otherwise redact the string
                    var pattern = Source.RedactString(chunk);
                    valueParts.Add(new ValuePart(pattern, Source, true));
                }
            }
        }
    }
}
