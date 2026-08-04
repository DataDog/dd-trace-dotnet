// <copyright file="FlagDictionaryJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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

        var result = existingValue ?? new Dictionary<string, Flag>();
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
}
