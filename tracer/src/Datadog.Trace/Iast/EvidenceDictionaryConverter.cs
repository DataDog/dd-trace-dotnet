// <copyright file="EvidenceDictionaryConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

/// <summary>
/// Serialize <see cref="Datadog.Trace.Iast.Evidence"/> struct to a dictionary
/// </summary>
internal sealed class EvidenceDictionaryConverter
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

    public EvidenceDictionaryConverter(int maxValueLength, bool redactionEnabled)
    {
        _redactionEnabled = redactionEnabled;
        _maxValueLength = maxValueLength;
    }

    public Dictionary<string, object> EvidenceToDictionary(Evidence evidence)
    {
        var result = new Dictionary<string, object>();

        if (evidence.Ranges == null || evidence.Ranges.Length == 0)
        {
            TruncationUtils.InsertTruncatableValue(result, "value", evidence.Value, _maxValueLength);
        }
        else
        {
            var valueParts = new List<object>(evidence.Ranges.Length);
            if (_redactionEnabled)
            {
                result["valueParts"] = CreateRedactedValueParts(valueParts, evidence.Value!, evidence.Ranges, evidence.Sensitive);
            }
            else
            {
                result["valueParts"] = CreateTaintedValueParts(valueParts, evidence.Value!, evidence.Ranges);
            }
        }

        return result;
    }

    private List<object> CreateTaintedValueParts(List<object> valueParts, string value, Range[] ranges)
    {
        var start = 0;
        foreach (var range in ranges)
        {
            if (range.Start > start)
            {
                valueParts.Add(CreateValuePart(value.Substring(start, range.Start - start)));
            }

            var substring = EvidenceConverterHelper.Substring(value, range);
            valueParts.Add(CreateValuePart(substring, range.Source));
            start = range.Start + substring.Length;
        }

        if (start < value.Length)
        {
            valueParts.Add(CreateValuePart(value.Substring(start)));
        }

        return valueParts;
    }

    private Dictionary<string, object> CreateValuePart(string value, Source? source = null)
    {
        var result = new Dictionary<string, object>();
        TruncationUtils.InsertTruncatableValue(result, "value", value, _maxValueLength);

        if (source != null)
        {
            result["source"] = source.GetInternalId();
        }

        return result;
    }

    private Dictionary<string, object> CreateRedactedValuePart(string? value, Source? source = null)
    {
        var result = new Dictionary<string, object> { { "redacted", true } };
        TruncationUtils.InsertTruncatableValue(result, "pattern", value, _maxValueLength);

        if (source != null)
        {
            result["source"] = source.GetInternalId();
        }

        return result;
    }

    private void ProcessValuePart(EvidenceConverterHelper.ValuePart? part, List<object> valueParts)
    {
        if (part == null)
        {
            return;
        }

        if (part.Value is { IsRedacted: true })
        {
            valueParts.Add(CreateRedactedValuePart(part.Value.Value, part.Value.Source));
        }
        else if (part.Value is { ShouldRedact: false, Value: not null })
        {
            valueParts.Add(CreateValuePart(part.Value.Value, part.Value.Source));
        }
        else
        {
            foreach (var valuePart in part.Value.Split())
            {
                if (valuePart.Value is not null)
                {
                    ProcessValuePart(valuePart, valueParts);
                }
            }
        }
    }

    private List<object> CreateRedactedValueParts(List<object> valueParts, string value, Range[] ranges, Range[]? sensitiveRanges)
    {
        var tainted = new LinkedList<Range>(ranges);
        var sensitive = sensitiveRanges != null ? new LinkedList<Range>(sensitiveRanges) : new LinkedList<Range>();

        var parts = new EvidenceConverterHelper.ValuePartIterator(value, tainted, sensitive);
        foreach (var part in parts)
        {
            if (part == null)
            {
                continue;
            }

            ProcessValuePart(part, valueParts);
        }

        return valueParts;
    }
}
