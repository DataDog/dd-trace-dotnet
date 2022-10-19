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

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Value, Ranges);
    }
}
