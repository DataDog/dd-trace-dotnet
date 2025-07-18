// <copyright file="SpanEventConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Util;

internal class SpanEventConverter : JsonConverter<SpanEvent>
{
    public override bool CanRead => false; // don't need to read the JSON only write

    public override void WriteJson(JsonWriter writer, SpanEvent value, JsonSerializer serializer)
    {
        var eventJObject = new JObject();
        eventJObject.Add("name", value.Name);
        eventJObject.Add("time_unix_nano", value.Timestamp.ToUnixTimeNanoseconds());

        // allowed types as values: string, bool, or some numeric type
        // note that char wasn't listed in RFC, but putting it here as it is like a string
        // allow any dimension of those primitive types above
        if (value.Attributes is not null)
        {
            var acceptedAttr = new List<KeyValuePair<string, object>>();
            // while we _can_ serialize most objects we aren't supposed to
            foreach (var kvp in value.Attributes)
            {
                if (!string.IsNullOrEmpty(kvp.Key)
                    && IsAllowedType(kvp.Value))
                {
                    acceptedAttr.Add(kvp);
                }
            }

            if (acceptedAttr.Count > 0)
            {
                var jObject = new JObject(acceptedAttr.Select(kvp => new JProperty(kvp.Key, JToken.FromObject(kvp.Value))));
                eventJObject.Add("attributes", jObject);
            }
        }

        writer.WriteToken(eventJObject.CreateReader());
    }

    public override SpanEvent ReadJson(JsonReader reader, Type objectType, SpanEvent existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    internal static bool IsAllowedType(object value)
    {
        if (value is null)
        {
            return false;
        }

        if (value is Array array)
        {
            if (array.Length == 0 ||
                array.Rank > 1)
            {
                // Newtonsoft doesn't seem to support multidimensional arrays (e.g., [,]), but does support jagged (e.g., [][])
                return false;
            }

            if (value.GetType() is { } type
                && type.IsArray
                && type.GetElementType() == typeof(object))
            {
                // Arrays may only have a primitive type, not 'object'
                return false;
            }

            value = array.GetValue(0);

            if (value is null)
            {
                return false;
            }
        }

        // Removed decimal & ulong from supported types to match both Native SpanEvents RFC and JSON events
        return (value is string or bool ||
                value is char ||
                value is sbyte ||
                value is byte ||
                value is ushort ||
                value is short ||
                value is uint ||
                value is int ||
                value is long ||
                value is float ||
                value is double);
    }
}
