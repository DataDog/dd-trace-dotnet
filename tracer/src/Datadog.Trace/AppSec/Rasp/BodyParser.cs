// <copyright file="BodyParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Rasp;

internal static class BodyParser
{
    private const int MaxElements = 1000;
    private const int MaxDepth = 64;
    private const int MaxStringSize = 1024;

    public static object? Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader);

        if (!jsonReader.Read())
        {
            return null;
        }

        State state = new();
        return ReadValue(jsonReader, ref state, 0);
    }

    private static object? ReadValue(JsonTextReader r, ref State state, int depth)
    {
        if (depth >= MaxDepth)
        {
            state.ObjectTooDeep = true;
            r.Skip();
            return null;
        }

        if (state.ElemsLeft-- <= 0)
        {
            state.ListMapTooLarge = true;
            r.Skip();
            return null;
        }

        return r.TokenType switch
        {
            JsonToken.StartObject => ReadObject(r, ref state, depth),
            JsonToken.StartArray => ReadArray(r, ref state, depth),
            JsonToken.String => ReadString(r, ref state),
            JsonToken.Integer => r.Value != null ? Convert.ToDouble(r.Value) : null,
            JsonToken.Float => r.Value != null ? Convert.ToDouble(r.Value) : null,
            JsonToken.Boolean => r.Value,
            JsonToken.Null => null,
            _ => null
        };
    }

    private static string? ReadString(JsonTextReader r, ref State state)
    {
        string? val = r.Value?.ToString();
        if (val != null && val.Length > MaxStringSize)
        {
            state.StringTooLong = true;
            val = val.Substring(0, MaxStringSize);
        }

        return val;
    }

    private static Dictionary<string, object?> ReadObject(JsonTextReader r, ref State state, int depth)
    {
        var dict = new Dictionary<string, object?>();

        while (r.Read() && r.TokenType != JsonToken.EndObject)
        {
            if (r.TokenType == JsonToken.PropertyName)
            {
                string propertyName = r.Value?.ToString() ?? string.Empty;

                if (r.Read())
                {
                    object? val = ReadValue(r, ref state, depth + 1);
                    if (!state.ListMapTooLarge)
                    {
                        dict[propertyName] = val;
                    }
                }
            }
        }

        return dict;
    }

    private static List<object?> ReadArray(JsonTextReader r, ref State state, int depth)
    {
        var list = new List<object?>();

        while (r.Read() && r.TokenType != JsonToken.EndArray)
        {
            object? val = ReadValue(r, ref state, depth + 1);
            if (!state.ListMapTooLarge)
            {
                list.Add(val);
            }
        }

        return list;
    }

    public record struct State(
        int ElemsLeft = MaxElements,
        bool ObjectTooDeep = false,
        bool ListMapTooLarge = false,
        bool StringTooLong = false);
}
