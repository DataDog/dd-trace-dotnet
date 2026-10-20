// <copyright file="ExperimentationOptOut.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Datadog.Trace.FeatureFlags;

internal static class ExperimentationOptOut
{
    internal const string Attribute = "dd_experimentation_opt_out_at";
    private static readonly Regex Timestamp = new(
        @"\A([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]+)?(?:Z|[+-]([0-9]{2}):([0-9]{2}))\z",
        RegexOptions.CultureInvariant);

    internal static bool TryExclude(IDictionary<string, object?>? attributes, out bool exclude)
    {
        exclude = false;
        if (attributes is null || !attributes.TryGetValue(Attribute, out var value))
        {
            return true;
        }

        if (value is not string timestamp)
        {
            return false;
        }

        var match = Timestamp.Match(timestamp);
        if (!match.Success)
        {
            return false;
        }

        var year = Number(1);
        var month = Number(2);
        var day = Number(3);
        // The wire format permits year zero. It has the same leap-year rule as year 2000.
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year == 0 ? 2000 : year, month)
         || Number(4) >= 24 || Number(5) >= 60 || Number(6) >= 60
         || (match.Groups[7].Success && (Number(7) >= 24 || Number(8) >= 60)))
        {
            return false;
        }

        exclude = true;
        return true;

        int Number(int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }
}
