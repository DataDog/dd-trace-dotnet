// <copyright file="NullTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration;

internal class NullTracerSettings : ITracerSettings
{
    public static readonly NullTracerSettings Instance = new();

    private NullTracerSettings()
    {
    }

    public bool TryGetObject(string key, out object? value)
    {
        value = default;
        return false;
    }

    public bool TryGetInt(string key, out int value)
    {
        value = default;
        return false;
    }

    public bool TryGetNullableDouble(string key, out double? value)
    {
        value = default;
        return false;
    }

    public bool TryGetBool(string key, out bool value)
    {
        value = default;
        return false;
    }

    public bool TryGetDouble(string key, out double value)
    {
        value = default;
        return false;
    }

    public bool TryGetNullableInt(string key, out int? value)
    {
        value = default;
        return false;
    }

    public bool TryGetNullableBool(string key, out bool? value)
    {
        value = default;
        return false;
    }
}
