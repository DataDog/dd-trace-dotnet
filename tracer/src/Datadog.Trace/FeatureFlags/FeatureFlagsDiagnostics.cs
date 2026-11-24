// <copyright file="FeatureFlagsDiagnostics.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Formats raw evaluation inputs and outputs for the debug log, so a customer can explain a flag
/// result from the log alone. Callers check the debug level first: nothing here is free.
/// <para>
/// Plain values are written in full, never truncated, and nothing here throws: a value that cannot be
/// formatted is written as a placeholder naming its type, so a diagnostic can never change what
/// an evaluation returns.
/// </para>
/// </summary>
internal static class FeatureFlagsDiagnostics
{
    /// <summary>
    /// Formats a flag value, default value or context attribute. Strings are quoted, so an empty
    /// string is told apart from a missing value.
    /// </summary>
    internal static string FormatValue(object? value)
    {
        // G17/G9 preserve double/float precision on every supported runtime, including .NET Framework
        // where "R" does not always round-trip doubles. Maps and arrays are the shapes the evaluator
        // produces for JSON values. Anything else, such as an OpenFeature Value, is named rather than
        // serialized by reflection, which writes every property and repeats nested structures at each level.
        try
        {
            return value switch
            {
                null => "null",
                string s => JsonConvert.ToString(s),
                bool b => b ? "true" : "false",
                double d => d.ToString("G17", CultureInfo.InvariantCulture),
                float f => f.ToString("G9", CultureInfo.InvariantCulture),
                DateTime dt => JsonConvert.ToString(dt),
                IConvertible c when c.GetTypeCode() is >= TypeCode.SByte and <= TypeCode.Decimal => c.ToString(CultureInfo.InvariantCulture),
                IEnumerable<KeyValuePair<string, object?>> map => FormatMap(map),
                object?[] items => FormatArray(items),
                _ => $"<not formatted: {value!.GetType().FullName}>",
            };
        }
        catch (Exception ex)
        {
            return $"<unformattable {value!.GetType().FullName}: {ex.GetType().Name}>";
        }
    }

    /// <summary>
    /// Formats context attributes or flag metadata as a JSON-like object. Each entry is formatted on
    /// its own, so one value that cannot be formatted does not hide the others.
    /// Unlike JsonHelper.SerializeObject, this only formats supported value shapes and does not
    /// reflect over arbitrary objects or invoke their property getters.
    /// </summary>
    internal static string FormatMap<T>(IEnumerable<KeyValuePair<string, T>>? map)
    {
        if (map is null)
        {
            return "{}";
        }

        var sb = StringBuilderCache.Acquire();
        try
        {
            using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using var writer = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared };
            writer.WriteStartObject();
            foreach (var pair in map)
            {
                writer.WritePropertyName(pair.Key);
                if (pair.Value is null or string)
                {
                    // Flag metadata contains only strings; the writer handles these directly.
                    writer.WriteValue(pair.Value);
                }
                else
                {
                    // Context attributes can also contain structured values and OpenFeature objects.
                    writer.WriteRawValue(FormatValue(pair.Value));
                }
            }

            writer.WriteEndObject();
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"<unformattable: {ex.GetType().Name}>";
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }

    private static string FormatArray(object?[] items)
    {
        var sb = StringBuilderCache.Acquire();
        try
        {
            using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using var writer = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared };
            writer.WriteStartArray();
            foreach (var item in items)
            {
                writer.WriteRawValue(FormatValue(item));
            }

            writer.WriteEndArray();
            return sb.ToString();
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }

    /// <summary>
    /// Logs, at debug level, every flag of an applied configuration that was rejected. A rejected
    /// flag resolves to its default value with PARSE_ERROR, so this is what explains it.
    /// </summary>
    internal static void LogRejectedFlags(IDatadogLogger log, string source, ServerConfiguration configuration)
    {
        if (configuration.Flags is null || !log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        var rejected = configuration.Flags.InvalidFlagKeys.ToList();
        if (rejected.Count == 0)
        {
            return;
        }

        log.Debug<int, string, string>(
            "Feature Flags: {RejectedFlagCount} flag(s) in the {Source} configuration were rejected and resolve to their default value with PARSE_ERROR: {RejectedFlags}",
            rejected.Count,
            source,
            string.Join(", ", rejected));
    }
}
