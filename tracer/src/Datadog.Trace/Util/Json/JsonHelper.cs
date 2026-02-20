// <copyright file="JsonHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Vendors.Newtonsoft.Json;

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
