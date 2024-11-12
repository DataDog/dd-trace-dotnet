// <copyright file="OverrideValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Configuration;

internal readonly struct OverrideValue<T>
{
    public readonly T Initial;
    public readonly bool IsOverridden;

    private readonly T _override;

    public OverrideValue(T initial)
    {
        Initial = initial;
        _override = default!;
        IsOverridden = false;
    }

    public OverrideValue(T initial, T @override)
    {
        Initial = initial;
        _override = @override;
        IsOverridden = true;
    }

    public T Value => IsOverridden ? _override : Initial;

    public OverrideValue<T> Override(T value)
        => new(Initial, value);
}
