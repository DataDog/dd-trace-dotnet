// <copyright file="FeatureListJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

/// <summary>
/// Reads a split's <c>features</c> list. Features only feed exposure routing and flag metadata, so
/// a malformed list or entry is dropped instead of rejecting the flag: parsing must never change
/// which variation a subject gets.
/// </summary>
internal sealed class FeatureListJsonConverter : JsonConverter<List<Feature>?>
{
    public override List<Feature>? ReadJson(
        JsonReader reader,
        Type objectType,
        List<Feature>? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartArray)
        {
            reader.Skip();
            return null;
        }

        var features = new List<Feature>();
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            var feature = ReadFeature(reader);
            if (feature is not null)
            {
                features.Add(feature);
            }
        }

        return features;
    }

    public override void WriteJson(JsonWriter writer, List<Feature>? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();
        foreach (var feature in value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("key");
            writer.WriteValue(feature.Key);
            writer.WritePropertyName("value");
            writer.WriteValue(feature.Value);
            writer.WritePropertyName("destinations");
            writer.WriteStartArray();
            foreach (var destination in feature.Destinations)
            {
                writer.WriteValue(destination);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static Feature? ReadFeature(JsonReader reader)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? key = null;
        object? value = null;
        List<string>? destinations = null;

        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            switch ((string?)reader.Value)
            {
                case "key":
                    reader.Read();
                    if (reader.TokenType == JsonToken.String)
                    {
                        key = (string?)reader.Value;
                    }
                    else
                    {
                        reader.Skip();
                    }

                    break;
                case "value":
                    reader.Read();
                    value = ReadScalar(reader);
                    break;
                case "destinations":
                    reader.Read();
                    destinations = ReadDestinations(reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (key is not { Length: > 0 } || value is null || destinations is not { Count: > 0 })
        {
            return null;
        }

        return new Feature(key, value, destinations);
    }

    private static object? ReadScalar(JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonToken.String:
                return (string?)reader.Value;
            case JsonToken.Integer:
            case JsonToken.Float:
                // An integer outside the long range arrives as a BigInteger, which is not IConvertible;
                // skip that feature rather than fail the whole configuration.
                return reader.Value is IConvertible number ? Convert.ToDouble(number, CultureInfo.InvariantCulture) : null;
            case JsonToken.Boolean:
                return (bool)reader.Value!;
            case JsonToken.Date:
                // Fallback only: both delivery paths use DateParseHandling.None. A round-trip string keeps the
                // instant, not always the text.
                return reader.Value switch
                {
                    DateTimeOffset offset => offset.ToString("o", CultureInfo.InvariantCulture),
                    DateTime date => date.ToString("o", CultureInfo.InvariantCulture),
                    _ => null,
                };
            default:
                reader.Skip();
                return null;
        }
    }

    private static List<string>? ReadDestinations(JsonReader reader)
    {
        if (reader.TokenType != JsonToken.StartArray)
        {
            reader.Skip();
            return null;
        }

        List<string>? destinations = null;
        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            if (reader.TokenType == JsonToken.String && ((string?)reader.Value) is { Length: > 0 } destination)
            {
                destinations ??= new List<string>();
                destinations.Add(destination);
            }
            else
            {
                reader.Skip();
            }
        }

        return destinations;
    }
}
