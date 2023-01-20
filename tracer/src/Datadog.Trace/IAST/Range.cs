// <copyright file="Range.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast;

internal readonly struct Range
{
    public Range(int start, int length, Source? source)
    {
        this.Start = start;
        this.Length = length;
        this.Source = source;
    }

    public int Start { get; }

    public int Length { get; }

    public Source? Source { get; }

    public override int GetHashCode()
    {
        return IastUtils.GetHashCode(Start, Length, Source);
    }
}
