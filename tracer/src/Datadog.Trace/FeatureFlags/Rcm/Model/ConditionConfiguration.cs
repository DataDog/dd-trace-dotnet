// <copyright file="ConditionConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class ConditionConfiguration
{
    private Regex? _regex;
    private OptionalField<string> _attribute;
    private OptionalField<FlagEvaluationConfiguration> _flagEvaluation;

    public ConditionOperator? Operator { get; set; }

    public string? Attribute
    {
        get => _attribute.Value;
        set => _attribute = new OptionalField<string>(value);
    }

    public FlagEvaluationConfiguration? FlagEvaluation
    {
        get => _flagEvaluation.Value;
        set => _flagEvaluation = new OptionalField<FlagEvaluationConfiguration>(value);
    }

    internal OptionalField<string> AttributeField => _attribute;

    internal OptionalField<FlagEvaluationConfiguration> FlagEvaluationField => _flagEvaluation;

    public object? Value { get; set; }

    public bool ShouldSerializeAttribute() => _attribute.IsPresent;

    public bool ShouldSerializeFlagEvaluation() => _flagEvaluation.IsPresent;

    internal bool TryValidateSource(out bool isDependency, out string? dependencyKey)
    {
        dependencyKey = null;
        isDependency = _flagEvaluation.IsPresent;
        if (_attribute.IsPresent == isDependency)
        {
            return false;
        }

        if (!isDependency)
        {
            return true;
        }

        var descriptor = _flagEvaluation.Value;
        if (Operator is not ConditionOperator.ONE_OF and not ConditionOperator.NOT_ONE_OF
         || descriptor is null
         || !descriptor.KeyField.IsPresent
         || descriptor.Key is not string { Length: > 0 } validatedKey
         || Value is string
         || Value is not IEnumerable values)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value is not string && value is not JValue { Type: JTokenType.String })
            {
                return false;
            }
        }

        dependencyKey = validatedKey;
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

            if (pattern.StartsWith("(?u)", StringComparison.Ordinal))
            {
                pattern = pattern.Substring(4);
            }

            pattern = pattern.Replace("[:alnum:]", @"\p{L}\p{N}");

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
