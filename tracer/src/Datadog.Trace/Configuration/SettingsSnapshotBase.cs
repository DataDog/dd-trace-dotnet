// <copyright file="SettingsSnapshotBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

internal class SettingsSnapshotBase
{
    protected static readonly HashSet<string> EmptyHashSet = new();

    protected static Dictionary<string, string>? GetDictionary(IDictionary<string, string>? source)
    {
        if (source is null or { Count: 0 })
        {
            return null;
        }

        return new Dictionary<string, string>(source);
    }

    protected static HashSet<string>? GetHashSet(HashSet<string>? source)
    {
        if (source is null or { Count: 0 })
        {
            return null;
        }

        return new HashSet<string>(source);
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            telemetry.Record(key, newValue, recordValue: true, ConfigurationOrigins.Code);
        }
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, int? oldValue, int? newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, double? oldValue, double? newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, bool oldValue, bool newValue)
    {
        if (oldValue != newValue)
        {
            telemetry.Record(key, newValue, ConfigurationOrigins.Code);
        }
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, bool? oldValue, bool? newValue)
    {
        if (oldValue != newValue)
        {
            if (newValue is null)
            {
                telemetry.Record(key, null, recordValue: true, ConfigurationOrigins.Code);
            }
            else
            {
                telemetry.Record(key, newValue.Value, ConfigurationOrigins.Code);
            }
        }
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, IDictionary<string, string>? oldValues, IDictionary<string, string>? newValues)
    {
        if (oldValues is null
         && (newValues is null || newValues is { Count: 0 }))
        {
            return;
        }

        var equal = oldValues is not null
                 && newValues is not null
                 && oldValues.Count == newValues.Count;

        if (equal)
        {
            foreach (var kvp in oldValues!)
            {
                if (!newValues!.TryGetValue(kvp.Key, out var newValue)
                 || !string.Equals(kvp.Value, newValue, StringComparison.Ordinal))
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            return;
        }

        var telemetryValue = newValues switch
        {
            null => null,
            { Count: 0 } => string.Empty,
            _ => string.Join(",", newValues.Select(x => $"{x.Key}:{x.Value}")),
        };

        telemetry.Record(key, telemetryValue, recordValue: true, ConfigurationOrigins.Code);
    }

    protected static void RecordIfChanged(IConfigurationTelemetry telemetry, string key, HashSet<string>? oldValues, HashSet<string>? newValues)
    {
        if (oldValues is null
         && (newValues is null || newValues is { Count: 0 }))
        {
            return;
        }

        var equal = oldValues is not null
                 && newValues is not null
                 && oldValues.Count == newValues.Count;

        if (equal)
        {
            foreach (var oldValue in oldValues!)
            {
                if (!newValues!.Contains(oldValue))
                {
                    equal = false;
                    break;
                }
            }
        }

        if (equal)
        {
            return;
        }

        var telemetryValue = newValues switch
        {
            null => null,
            { Count: 0 } => string.Empty,
            _ => string.Join(",", newValues),
        };

        telemetry.Record(key, telemetryValue, recordValue: true, ConfigurationOrigins.Code);
    }
}
