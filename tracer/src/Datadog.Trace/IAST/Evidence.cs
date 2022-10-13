// <copyright file="Evidence.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Evidence
{
    public Evidence(string value, Range[]? ranges = null)
    {
        this.Value = value;
        this.Ranges = ranges;
    }

    public string Value { get; }

    public Range[]? Ranges { get; }

    public bool Equals(Evidence other)
    {
        if (Ranges?.Length != other.Ranges?.Length)
        {
            return false;
        }

        for (int i = 0; i < Ranges?.Length; i++)
        {
            if (!Ranges[i].Equals(other.Ranges?[i]))
            {
                return false;
            }
        }

        return Value.Equals(other.Value);
    }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Value, Ranges);
    }
}
