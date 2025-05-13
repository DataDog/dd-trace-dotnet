// <copyright file="IntegrationSettingsSerializationHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

internal static class IntegrationSettingsSerializationHelper
{
    public static object?[] SerializeFromAutomatic(
        bool? enabled,
        bool? analyticsEnabled,
        double analyticsSampleRate)
        => [enabled, analyticsEnabled, analyticsSampleRate];

    public static bool TryDeserializeFromAutomatic(
        object?[] values,
        out bool? enabled,
        out bool? analyticsEnabled,
        out double analyticsSampleRate)
    {
        if (values is null || values.Length < 3)
        {
            // this will never happen unless there's a bad version mismatch issue, so just bail out
            enabled = null;
            analyticsEnabled = null;
            analyticsSampleRate = 1.0;
            return false;
        }

        enabled = values[0] as bool?;
        analyticsEnabled = values[1] as bool?;
        analyticsSampleRate = values[2] as double? ?? 1.0;
        return true;
    }

    public static object?[]? SerializeFromManual(
        bool enabledChanged,
        bool? enabled,
        bool analyticsEnabledChanged,
        bool? analyticsEnabled,
        bool analyticsSampleRateChanged,
        double? analyticsSampleRate)
    {
        if (enabledChanged || analyticsEnabledChanged || analyticsSampleRateChanged)
        {
            // we have changes, record everything
            // Yes, this is a lot of boxing :(
            return
            [
                enabledChanged,
                enabled,
                analyticsEnabledChanged,
                analyticsEnabled,
                analyticsSampleRateChanged,
                analyticsSampleRate,
            ];
        }

        // no changes
        return null;
    }

    public static bool TryDeserializeFromManual(
        object?[] values,
        out bool enabledChanged,
        out bool? enabled,
        out bool analyticsEnabledChanged,
        out bool? analyticsEnabled,
        out bool analyticsSampleRateChanged,
        out double analyticsSampleRate)
    {
        if (values is not { Length: 6 })
        {
            // bad version mismatch issue, so just bail out
            enabledChanged = false;
            enabled = null;
            analyticsEnabledChanged = false;
            analyticsEnabled = null;
            analyticsSampleRateChanged = false;
            analyticsSampleRate = 1.0;
            return false;
        }

        enabledChanged = values[0] is true;
        enabled = values[1] as bool?;

        analyticsEnabledChanged = values[2] is true;
        analyticsEnabled = values[3] as bool?;

        if (values[4] is true && values[5] is double rate)
        {
            analyticsSampleRateChanged = true;
            analyticsSampleRate = rate;
        }
        else
        {
            analyticsSampleRateChanged = false;
            analyticsSampleRate = 1.0;
        }

        return true;
    }
}
