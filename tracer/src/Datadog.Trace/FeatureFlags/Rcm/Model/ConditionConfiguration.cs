// <copyright file="ConditionConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ConditionConfiguration
{
    private Regex? _regex = null;

    public ConditionOperator? Operator { get; set; }

    public string? Attribute { get; set; }

    public object? Value { get; set; }

    internal bool MatchesRegex(object attributeValue)
    {
        if (_regex == null)
        {
            var pattern = Value?.ToString() ?? string.Empty;
            if (pattern is not { Length: > 0 })
            {
                throw new FormatException("Condition value can not be null nor empty");
            }

            _regex = new Regex(pattern, RegexOptions.Compiled);
        }

        try
        {
            return _regex.IsMatch(ToString(attributeValue));
        }
        catch
        {
            return false;
        }

        static string ToString(object attributeValue)
        {
            if (attributeValue is null) { return string.Empty; }
            if (attributeValue is bool boolValue) { return boolValue ? "true" : "false"; }
            return Convert.ToString(attributeValue, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
