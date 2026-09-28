// <copyright file="StrictBooleanTrueJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class StrictBooleanTrueJsonConverter : JsonConverter<bool>
{
    public override bool ReadJson(JsonReader reader, Type objectType, bool existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        // Malformed consent protects data without rejecting otherwise usable flag configuration.
        var consent = reader.TokenType == JsonToken.Boolean && reader.Value is true;
        reader.Skip();
        return consent;
    }

    public override void WriteJson(JsonWriter writer, bool value, JsonSerializer serializer) => writer.WriteValue(value);
}
