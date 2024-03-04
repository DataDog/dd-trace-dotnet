// <copyright file="OverrideValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal readonly struct OverrideValue<T>
{
    public readonly T Value;
    public readonly bool IsOverridden;

    public OverrideValue(T value)
    {
        Value = value;
        IsOverridden = true;
    }

    public OverrideValue()
    {
        Value = default!;
        IsOverridden = false;
    }
}
