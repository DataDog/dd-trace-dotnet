// <copyright file="SourceConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast.SensitiveData;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json.Utilities;

namespace Datadog.Trace.Iast;

/// <summary>
/// Custom JSON serializer for <see cref="Datadog.Trace.Iast.Source"/> struct
/// </summary>
internal class SourceConverter : JsonConverter<Source>
{
    // When not redacted output is:
    // { "origin": "http.request.parameter.name", "name": "name", "value": "value" }
    //
    // When redacted output is:
    // { "origin": "http.request.parameter.name", "name": "name", "redacted": true }

    private int _maxValueLength;

    public SourceConverter(int maxValueLength)
    {
        _maxValueLength = maxValueLength;
    }

    public override bool CanRead => true;

    public override Source? ReadJson(JsonReader reader, Type objectType, Source? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        // { "origin": "http.request.parameter", "name": "$1", "value": "table" }
        JObject jo = JObject.Load(reader);
        var origin = SourceTypeUtils.FromString(jo["origin"]?.ToString());
        var name = jo["name"]?.ToString();
        var value = jo["value"]?.ToString();

        return new Source(origin, name, value);
    }

    public override void WriteJson(JsonWriter writer, Source? source, JsonSerializer serializer)
    {
        if (source != null)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("origin");
            writer.WriteValue(SourceTypeUtils.GetString(source.Origin));
            if (source.Name != null)
            {
                writer.WritePropertyName("name");
                writer.WriteValue(source.Name);
            }

            if (source.IsRedacted && source.Value != null)
            {
                writer.WritePropertyName("redacted");
                writer.WriteValue(true);
                if (source.RedactedValue != null)
                {
                    writer.WritePropertyName("pattern");
                    writer.WriteTruncatableValue(source.RedactedValue, _maxValueLength);
                }
            }
            else if (source.Value != null)
            {
                writer.WritePropertyName("value");
                writer.WriteTruncatableValue(source.Value, _maxValueLength);
            }

            writer.WriteEndObject();
        }
    }
}
