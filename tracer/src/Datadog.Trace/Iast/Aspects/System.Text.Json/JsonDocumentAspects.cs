// <copyright file="JsonDocumentAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK && !NETSTANDARD2_0
using System;
using System.Text.Json;
using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.Text.Json;

/// <summary> Xpath injection class aspect </summary>
[AspectClass("System.Text.Json", AspectType.Source)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class JsonDocumentAspects
{
    /// <summary>
    /// Taint all string values from deserialized JSON
    /// </summary>
    /// <param name="json">the JsonDocument result of Parse</param>
    /// <param name="options">the JsonDocumentOptions</param>
    /// <returns>the JsonDocument result</returns>
    [AspectMethodReplace("System.Text.Json.JsonDocument::Parse(System.String,System.Text.Json.JsonDocumentOptions)")]
    public static object Parse(string json, JsonDocumentOptions options)
    {
        var doc = JsonDocument.Parse(json, options);
        TaintJsonElements(json, doc);
        return doc;
    }

    /// <summary>
    /// GetString method aspect
    /// </summary>
    /// <param name="target">the JsonElement instance</param>
    /// <returns>the string result</returns>
    [AspectMethodReplace("System.Text.Json.JsonElement::GetString()", paramShift: [0], boxParam: [true])]
    public static string? GetString(object target)
    {
        Console.WriteLine(target);
        return string.Empty;
        /*
        var str = target.GetString();
        if (str is null)
        {
            return null;
        }

        var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
        var taintedTarget = taintedObjects?.Get(target);
        if (taintedObjects is not null && taintedTarget is not null)
        {
            taintedObjects.Taint(str, [new Range(0, str.Length)]);
        }

        return str;
        */
    }

    private static void TaintJsonElements(string json, JsonDocument doc)
    {
        var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
        var taintedTarget = taintedObjects?.Get(json);
        if (taintedObjects is null || taintedTarget is null)
        {
            return;
        }

        TaintStringElement(doc.RootElement, taintedObjects);
    }

    private static void TaintStringElement(JsonElement element, TaintedObjects map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    TaintStringElement(property.Value, map);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    TaintStringElement(item, map);
                }

                break;
            case JsonValueKind.String:
                map.Taint(element, [new Range(0, 0)]);
                break;
        }
    }
}
#endif
