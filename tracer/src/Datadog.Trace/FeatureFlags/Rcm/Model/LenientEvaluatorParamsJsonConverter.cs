// <copyright file="LenientEvaluatorParamsJsonConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Rcm.Model;

internal sealed class LenientEvaluatorParamsJsonConverter : JsonConverter<EvaluatorParams?>
{
    public override EvaluatorParams? ReadJson(JsonReader reader, Type objectType, EvaluatorParams? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonToken.StartObject)
        {
            return serializer.Deserialize<EvaluatorParams>(reader);
        }

        reader.Skip();
        return EvaluatorParams.Invalid;
    }

    public override void WriteJson(JsonWriter writer, EvaluatorParams? value, JsonSerializer serializer)
        => serializer.Serialize(writer, value);
}
