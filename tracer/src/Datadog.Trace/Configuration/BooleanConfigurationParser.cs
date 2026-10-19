// <copyright file="BooleanConfigurationParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Configuration;

// Shared with the independently packaged OpenFeature provider. Keep boolean configuration parsing consistent.
internal static class BooleanConfigurationParser
{
    public static bool? Parse(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length == 0)
        {
            return null;
        }

        if (value.Length == 1)
        {
            if (value[0] == 'T' || value[0] == 't' ||
                value[0] == 'Y' || value[0] == 'y' ||
                value[0] == '1')
            {
                return true;
            }

            if (value[0] == 'F' || value[0] == 'f' ||
                value[0] == 'N' || value[0] == 'n' ||
                value[0] == '0')
            {
                return false;
            }

            return null;
        }

        if (string.Equals(value, "TRUE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "FALSE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "NO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
