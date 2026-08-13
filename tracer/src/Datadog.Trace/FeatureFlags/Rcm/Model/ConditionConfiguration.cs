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
    private Regex? _regex;
    private ParsedSemVer? _semverComparand;

    public ConditionOperator? Operator { get; set; }

    public string? Attribute { get; set; }

    public object? Value { get; set; }

    /// <summary>
    /// Gets the validated, parsed SemVer condition value for SEMVER_* operators.
    /// Populated eagerly during config validation by <see cref="TryPreparseSemverComparand"/>.
    /// Null if the comparand is invalid or the operator is not a SEMVER_* operator.
    /// </summary>
    internal ParsedSemVer? SemverComparand => _semverComparand;

    /// <summary>
    /// Eagerly parses the SemVer comparand for SEMVER_* operators.
    /// Called during config validation, not during evaluation.
    /// Returns true if the comparand is valid or the operator is not a SEMVER_* operator.
    /// Returns false if the operator is a SEMVER_* operator and the comparand is invalid.
    /// </summary>
    internal bool TryPreparseSemverComparand()
    {
        if (Operator is ConditionOperator.SEMVER_EQ or ConditionOperator.SEMVER_NEQ or ConditionOperator.SEMVER_LT
            or ConditionOperator.SEMVER_LTE or ConditionOperator.SEMVER_GT or ConditionOperator.SEMVER_GTE)
        {
            if (Value is string comparand && SemVer.TryParse(comparand, out var parsed))
            {
                _semverComparand = parsed;
                return true;
            }

            return false;
        }

        return true;
    }

    internal bool MatchesRegex(object attributeValue)
    {
        if (_regex == null)
        {
            var pattern = Value?.ToString() ?? string.Empty;
            if (pattern is not { Length: > 0 })
            {
                throw new FormatException("Condition value can not be null nor empty");
            }

            try
            {
                _regex = new Regex(pattern, RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new FormatException($"Invalid regex pattern: {pattern}", ex);
            }
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
