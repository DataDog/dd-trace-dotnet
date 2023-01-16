// <copyright file="Evidence.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

internal readonly struct Evidence
{
    private readonly Range[]? _ranges;

    public Evidence(string value, Range[]? ranges = null)
    {
        this.Value = value;
        this._ranges = ranges;
    }

    public string Value { get; }

    // This method is only used once when serializing to json (and it is also called from unit tests)
    public List<ValuePart>? ValueParts => GetValuePartsFromRanges();

    private List<ValuePart>? GetValuePartsFromRanges()
    {
        if (_ranges is null || _ranges.Length == 0)
        {
            return null;
        }

        var valueParts = new List<ValuePart>();

        foreach (var range in _ranges)
        {
            valueParts.Add(new ValuePart(Value.Substring(range.Start, range.Length), range.Source.GetInternalId()));
        }

        return valueParts;
    }

    public Range[]? GetRanges()
    {
        return _ranges;
    }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Value, _ranges);
    }
}
