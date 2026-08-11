// <copyright file="FlagDictionaryJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class FlagDictionaryJsonConverter : JsonConverter<Dictionary<string, Flag>>
{
    public override Dictionary<string, Flag>? ReadJson(
        JsonReader reader,
        Type objectType,
        Dictionary<string, Flag>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        // A remote-config update is a complete snapshot. Never retain flags from the
        // previous dictionary when Newtonsoft asks the converter to reuse an instance.
        var result = new Dictionary<string, Flag>();
        var flags = JObject.Load(reader);
        foreach (var property in flags.Properties())
        {
            try
            {
                var flag = property.Value.ToObject<Flag>(serializer);
                result[property.Name] = flag is not null && IsValid(flag) ? flag : null!;
            }
            catch (JsonException)
            {
                result[property.Name] = null!;
            }
        }

        return result;
    }

    public override void WriteJson(JsonWriter writer, Dictionary<string, Flag>? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }

    private static bool IsValid(Flag flag)
    {
        if (flag.VariationType is null || flag.Variations is null)
        {
            return false;
        }

        if (flag.Variations.Values.Any(variant => variant is null || !MatchesType(Unwrap(variant.Value), flag.VariationType.Value)))
        {
            return false;
        }

        if (flag.Allocations is null)
        {
            return true;
        }

        foreach (var allocation in flag.Allocations)
        {
            if (allocation?.Splits is null)
            {
                return false;
            }

            foreach (var split in allocation.Splits)
            {
                if (split?.Shards is null)
                {
                    return false;
                }

                foreach (var shard in split.Shards)
                {
                    if (shard is null || shard.TotalShards <= 0 || shard.TotalShards > uint.MaxValue || shard.Ranges is null)
                    {
                        return false;
                    }

                    foreach (var range in shard.Ranges)
                    {
                        if (range is null || range.Start < 0 || range.Start >= range.End || range.End > shard.TotalShards)
                        {
                            return false;
                        }
                    }
                }
            }

            if (allocation.Rules is null)
            {
                continue;
            }

            foreach (var rule in allocation.Rules)
            {
                if (rule?.Conditions is null || rule.Conditions.Any(condition => !IsValid(condition)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsValid(ConditionConfiguration? condition)
    {
        if (condition?.Operator is null)
        {
            return false;
        }

        var value = Unwrap(condition.Value);
        switch (condition.Operator.Value)
        {
            case ConditionOperator.MATCHES:
            case ConditionOperator.NOT_MATCHES:
                if (value is not string pattern)
                {
                    return false;
                }

                try
                {
                    _ = new Regex(pattern.StartsWith("(?u)", StringComparison.Ordinal) ? pattern.Substring(4) : pattern);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }

            case ConditionOperator.LT:
            case ConditionOperator.LTE:
            case ConditionOperator.GT:
            case ConditionOperator.GTE:
                return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
            case ConditionOperator.ONE_OF:
            case ConditionOperator.NOT_ONE_OF:
                return condition.Value is JArray array && array.All(item => Unwrap(item) is string);
            case ConditionOperator.IS_NULL:
                return value is bool;
            case ConditionOperator.SEMVER_EQ:
            case ConditionOperator.SEMVER_NEQ:
            case ConditionOperator.SEMVER_LT:
            case ConditionOperator.SEMVER_LTE:
            case ConditionOperator.SEMVER_GT:
            case ConditionOperator.SEMVER_GTE:
                return SemanticVersion.TryParse(value, out _);
            default:
                return false;
        }
    }

    private static object? Unwrap(object? value) => value is JValue token ? token.Value : value;

    private static bool MatchesType(object? value, ValueType type) => type switch
    {
        ValueType.Boolean => value is bool,
        ValueType.String => value is string,
        ValueType.Integer => value is sbyte or byte or short or ushort or int or uint or long or ulong,
        ValueType.Numeric => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal,
        ValueType.Json => true,
        _ => false,
    };
}
