// <copyright file="FlagCollectionJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class FlagCollectionJsonConverter : JsonConverter<FlagCollection>
{
    public override FlagCollection? ReadJson(
        JsonReader reader,
        Type objectType,
        FlagCollection? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        var result = existingValue ?? new FlagCollection();
        var flags = JObject.Load(reader);
        foreach (var property in flags.Properties())
        {
            try
            {
                var flag = property.Value.ToObject<Flag>(serializer);
                if (flag is not null && IsValid(flag))
                {
                    result.Add(property.Name, flag);
                }
                else
                {
                    result.MarkInvalid(property.Name);
                }
            }
            catch (JsonException)
            {
                result.MarkInvalid(property.Name);
            }
        }

        return result;
    }

    public override void WriteJson(JsonWriter writer, FlagCollection? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        foreach (var pair in value.ValidFlags)
        {
            writer.WritePropertyName(pair.Key);
            serializer.Serialize(writer, pair.Value);
        }

        foreach (var key in value.InvalidFlagKeys)
        {
            writer.WritePropertyName(key);
            writer.WriteNull();
        }

        writer.WriteEndObject();
    }

    private static bool IsValid(Flag flag)
    {
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

            if (allocation.Rules is not null)
            {
                foreach (var rule in allocation.Rules)
                {
                    if (rule?.Conditions is null)
                    {
                        continue;
                    }

                    foreach (var condition in rule.Conditions)
                    {
                        if (condition is null || !HasValidOperand(condition))
                        {
                            return false;
                        }
                    }
                }
            }

            foreach (var split in allocation.Splits)
            {
                if (split?.Shards is null)
                {
                    return false;
                }

                foreach (var shard in split.Shards)
                {
                    if (shard is null || shard.TotalShards <= 0 || shard.Ranges is null)
                    {
                        return false;
                    }

                    foreach (var range in shard.Ranges)
                    {
                        if (range is null || range.Start < 0 || range.End < 0)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private static bool HasValidOperand(ConditionConfiguration condition)
    {
        if (condition.Operator is ConditionOperator.MATCHES or ConditionOperator.NOT_MATCHES)
        {
            return condition.HasValidRegex();
        }

        if (condition.Operator is ConditionOperator.ONE_OF or ConditionOperator.NOT_ONE_OF)
        {
            return condition.Value is JArray;
        }

        if (condition.Operator is ConditionOperator.SEMVER_EQ
                               or ConditionOperator.SEMVER_NEQ
                               or ConditionOperator.SEMVER_LT
                               or ConditionOperator.SEMVER_LTE
                               or ConditionOperator.SEMVER_GT
                               or ConditionOperator.SEMVER_GTE)
        {
            return condition.Value is string value && SemanticVersion.TryParse(value, out _);
        }

        return true;
    }
}
