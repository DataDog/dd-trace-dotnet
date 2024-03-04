// <copyright file="ManualInstrumentationSerializationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

namespace Datadog.Trace.Configuration;

/// <summary>
/// A helper class for serializing values to manual instrumentation
/// </summary>
internal class ManualInstrumentationSerializationHelper
{
    public static bool BuildIntegrationSettings(IntegrationSettingsCollection settings, out object? value)
    {
        if (settings.Settings.Length == 0)
        {
            value = null;
            return true;
        }

        var results = new Dictionary<string, object?[]>(settings.Settings.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings.Settings)
        {
            results[setting.IntegrationNameInternal] = IntegrationSettingsSerializationHelper.SerializeFromAutomatic(setting.EnabledInternal, setting.AnalyticsEnabledInternal, setting.AnalyticsSampleRateInternal);
        }

        value = results;
        return true;
    }

    public static bool BuildIntegrationSettings(ImmutableIntegrationSettingsCollection settings, out object? value)
    {
        if (settings.Settings.Length == 0)
        {
            value = null;
            return true;
        }

        var results = new Dictionary<string, object?[]>(settings.Settings.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in settings.Settings)
        {
            results[setting.IntegrationNameInternal] = IntegrationSettingsSerializationHelper.SerializeFromAutomatic(setting.EnabledInternal, setting.AnalyticsEnabledInternal, setting.AnalyticsSampleRateInternal);
        }

        value = results;
        return true;
    }

    public static bool Copy(IEnumerable<KeyValuePair<string, string>> setting, out object? value)
    {
        value = new ConcurrentDictionary<string, string>(setting);
        return true;
    }

    public static bool Found(object? setting, out object? value)
    {
        value = setting;
        return true;
    }

    public static bool Found(bool setting, out bool value)
    {
        value = setting;
        return true;
    }

    public static bool Found(int setting, out int value)
    {
        value = setting;
        return true;
    }

    public static bool Found(double? setting, out double? value)
    {
        value = setting;
        return true;
    }

    public static bool NotFound(out object? value)
    {
        value = null;
        return false;
    }

    public static bool NotFound(out bool value)
    {
        value = default;
        return false;
    }

    public static bool NotFound(out int value)
    {
        value = default;
        return false;
    }

    public static bool NotFound(out double value)
    {
        value = default;
        return false;
    }

    public static bool NotFound(out bool? value)
    {
        value = default;
        return false;
    }

    public static bool NotFound(out int? value)
    {
        value = default;
        return false;
    }

    public static bool NotFound(out double? value)
    {
        value = default;
        return false;
    }
}
