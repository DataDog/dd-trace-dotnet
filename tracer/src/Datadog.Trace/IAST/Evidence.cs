// <copyright file="Evidence.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;

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

        var valueParts = new List<ValuePart>(_ranges.Length);
        var rangeList = _ranges.ToList();
        rangeList.Sort();
        int currentIndex = 0;
        var valueLenght = Value.Length;
        foreach (var range in rangeList)
        {
            if (!range.IsEmpty() && range.Source != null && range.Start >= 0 && range.Start + range.Length <= valueLenght && currentIndex <= range.Start)
            {
                if (currentIndex != range.Start)
                {
                    valueParts.Add(new ValuePart(Value.Substring(currentIndex, range.Start - currentIndex), null));
                }

                valueParts.Add(new ValuePart(Value.Substring(range.Start, range.Length), range.Source.GetInternalId()));
                currentIndex = range.Start + range.Length;
            }
        }

        if (valueParts.Count > 0 && currentIndex < Value.Length)
        {
            valueParts.Add(new ValuePart(Value.Substring(currentIndex), null));
        }

        return valueParts.Count > 0 ? valueParts : null;
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
