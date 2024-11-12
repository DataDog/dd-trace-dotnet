// <copyright file="IntegrationSettingsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

namespace Datadog.Trace.Configuration;

internal sealed class IntegrationSettingsHelper
{
    public static IntegrationSettingsCollection ParseFromAutomatic(Dictionary<string, object?> initialValues)
    {
        var settings = Populate(initialValues, CreateSettingFunc);
        return new IntegrationSettingsCollection(settings);

        static IntegrationSettings CreateSettingFunc(string name, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
            => new(name, enabled, analyticsEnabled, analyticsSampleRate);
    }

    public static ImmutableIntegrationSettingsCollection ParseImmutableFromAutomatic(Dictionary<string, object?> initialValues)
    {
        var settings = Populate(initialValues, CreateSettingFunc);
        return new ImmutableIntegrationSettingsCollection(settings);

        static ImmutableIntegrationSettings CreateSettingFunc(string name, bool? enabled, bool? analyticsEnabled, double analyticsSampleRate)
            => new(name, enabled, analyticsEnabled, analyticsSampleRate);
    }

    private static Dictionary<string, T> Populate<T>(
        Dictionary<string, object?> initialValues,
        Func<string, bool?, bool?, double, T> createSettingFunc)
    {
        if (!initialValues.TryGetValue(TracerSettingKeyConstants.IntegrationSettingsKey, out var raw)
         || raw is not Dictionary<string, object?[]> fromAutomatic)
        {
            // happens when we're in manual-only instrumentation so won't have any configuration
            return new();
        }

        var settings = new Dictionary<string, T>(fromAutomatic.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var setting in fromAutomatic)
        {
            if (!IntegrationSettingsSerializationHelper.TryDeserializeFromAutomatic(
                    setting.Value,
                    out var enabled,
                    out var analyticsEnabled,
                    out var analyticsSampleRate))
            {
                // this will never happen unless there's a bad version mismatch issue, so just bail out
                return new();
            }

            settings.Add(
                setting.Key,
                createSettingFunc(
                    setting.Key,
                    enabled,
                    analyticsEnabled,
                    analyticsSampleRate));
        }

        return settings;
    }
}
