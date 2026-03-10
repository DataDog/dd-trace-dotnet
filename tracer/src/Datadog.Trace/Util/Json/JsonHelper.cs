// <copyright file="JsonHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Util.Json;

/// <summary>
/// Provides more optimized versions of <see cref="JsonConvert"/> APIs for serializing and deserializing,
/// using <see cref="JsonArrayPool"/> to reduce allocations.
/// </summary>
internal static class JsonHelper
{
    public static string SerializeObject(object? value)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject()
        var jsonSerializer = JsonSerializer.CreateDefault(settings: null);
        return SerializeObjectInternal(value, type: null, jsonSerializer);
    }

    public static string SerializeObject(object? value, JsonSerializerSettings settings)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject()
        var jsonSerializer = JsonSerializer.CreateDefault(settings);
        return SerializeObjectInternal(value, type: null, jsonSerializer);
    }

    public static string SerializeObject(object? value, Formatting formatting, JsonSerializerSettings settings)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject()
        var jsonSerializer = JsonSerializer.CreateDefault(settings);
        jsonSerializer.Formatting = formatting;
        return SerializeObjectInternal(value, type: null, jsonSerializer);
    }

    public static object? DeserializeObject(string value)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject()
        return DeserializeObject(value, type: null, settings: null);
    }

    public static T? DeserializeObject<T>(string value)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject()
        return (T?)DeserializeObject(value, typeof(T), settings: null);
    }

    public static JObject ParseJObject(byte[] json, Encoding encoding)
    {
        // A new overload, equivalent to calling Encoding.Utf8.GetBytes() and
        // passing that to Datadog.Trace.Vendors.Newtonsoft.Json.Linq.JObject.Parse()
        using var stream = new MemoryStream(json);
        using var streamReader = new StreamReader(stream, encoding);
        using var reader = new JsonTextReader(streamReader) { ArrayPool = JsonArrayPool.Shared };
        var o = JObject.Load(reader);
        while (reader.Read())
        {
            // validate no trailing content
        }

        return o;
    }

    public static JObject ParseJObject(string json)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.Linq.JObject.Parse()
        // differs from the vendored version, in that we use an array pool
        using var reader = new JsonTextReader(new StringReader(json)) { ArrayPool = JsonArrayPool.Shared };
        var o = JObject.Load(reader);
        while (reader.Read())
        {
            // validate no trailing content
        }

        return o;
    }

    public static string TokenToString(JToken token, Formatting formatting = Formatting.Indented, params JsonConverter[] converters)
    {
        // equivalent to Datadog.Trace.Vendors.Newtonsoft.Json.Linq.JToken.ToString()

        // differs from the vendored version, in that it uses our cache instead
        var sb = StringBuilderCache.Acquire();
        using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
        // differs from the vendored version, in that we use an array pool
        using (var jw = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared, Formatting = formatting })
        {
            token.WriteTo(jw, converters);
        }

        var result = sw.ToString();
        StringBuilderCache.Release(sb);
        return result;
    }

    private static string SerializeObjectInternal(object? value, System.Type? type, JsonSerializer jsonSerializer)
    {
        // differs from the vendored version, in that it uses our cache instead
        var sb = StringBuilderCache.Acquire();
        var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
        // differs from the vendored version, in that we use an array pool
        using (var jsonWriter = new JsonTextWriter(sw) { ArrayPool = JsonArrayPool.Shared })
        {
            jsonWriter.Formatting = jsonSerializer.Formatting;
            jsonSerializer.Serialize(jsonWriter, value, type);
        }

        var result = sw.ToString();
        StringBuilderCache.Release(sb);
        return result;
    }

    private static object? DeserializeObject(string value, System.Type? type, JsonSerializerSettings? settings)
    {
        var jsonSerializer = JsonSerializer.CreateDefault(settings);

        // by default DeserializeObject should check for additional content
        if (!jsonSerializer.IsCheckAdditionalContentSet())
        {
            jsonSerializer.CheckAdditionalContent = true;
        }

        // differs from the vendored version, in that we use an array pool
        using var reader = new JsonTextReader(new StringReader(value)) { ArrayPool = JsonArrayPool.Shared };
        return jsonSerializer.Deserialize(reader, type);
    }
}
