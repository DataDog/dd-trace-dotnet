// <copyright file="TracerSettingSerializationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;

namespace Datadog.Trace.Configuration;

internal static class TracerSettingSerializationHelper
{
    public static bool BuildIntegrationSettings(IntegrationSettingsCollection settings, out object? value)
    {
        if (settings.Settings.Count == 0)
        {
            value = default;
            return false;
        }

        var results = new Dictionary<string, object?[]>(settings.Settings.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in settings.Settings)
        {
            var setting = pair.Value;
            if (setting.GetChangeDetails() is { } changes)
            {
                results[setting.IntegrationName] = changes;
            }
        }

        value = results;
        return true;
    }

    public static bool IsChanged(in OverrideValue<IDictionary<string, string>> updated, out object? value)
    {
        if (HasChanges(in updated))
        {
            value = updated.Value;
            return true;
        }

        value = default;
        return false;

        static bool HasChanges(in OverrideValue<IDictionary<string, string>> updated)
        {
            // initial could be null, but value is never null
            var initial = updated.Initial;
            var value = updated.Value;

            // Currently need to account for customers _replacing_ the Global Tags as well as changing it
            // we create the updated one as a concurrent dictionary, so if it's not any more, then we know they've replaced it
            if (value is not ConcurrentDictionary<string, string> || (initial?.Count ?? 0) != value.Count)
            {
                return true;
            }

            if (initial is null)
            {
                return value.Count != 0;
            }

            var comparer = StringComparer.Ordinal;
            foreach (var kvp in initial)
            {
                if (!value.TryGetValue(kvp.Key, out var value2)
                 || !comparer.Equals(kvp.Value, value2))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static bool IsChanged(in OverrideValue<HashSet<string>> updated, out object? value)
    {
        // we always have an override, but are they the same?
        var initial = updated.Initial;
        var newValue = updated.Value;

        if (initial is null)
        {
            if (newValue.Count != 0)
            {
                value = newValue;
                return true;
            }
        }
        else
        {
            if ((initial.Count != newValue.Count) || !initial.SetEquals(newValue))
            {
                value = newValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    public static bool IfNotNull<T>(in T updated, out object? value)
        where T : class?
    {
        if (updated is not null)
        {
            value = updated;
            return true;
        }

        value = default;
        return false;
    }

    public static bool IsChanged<T>(in OverrideValue<T> updated, out object? value)
        where T : class?
    {
        if (updated.IsOverridden)
        {
            value = updated.Value;
            return true;
        }

        value = default;
        return false;
    }

    public static bool IsChanged<T>(in OverrideValue<T> updated, out T value)
        where T : struct
    {
        if (updated.IsOverridden)
        {
            value = updated.Value;
            return true;
        }

        value = default;
        return false;
    }

    public static bool IsChanged<T>(in OverrideValue<T?> updated, out T? value)
        where T : struct
    {
        if (updated.IsOverridden)
        {
            value = updated.Value;
            return true;
        }

        value = default;
        return false;
    }

    public static bool IsChanged(bool setting, out bool value)
    {
        value = setting;
        return true;
    }

    public static bool IsChanged(int setting, out int value)
    {
        value = setting;
        return true;
    }

    public static bool IsChanged(double? setting, out double? value)
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
