// <copyright file="JsonDocumentAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETCOREAPP
using System;
using System.Text.Json;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects.System.Text.Json;

/// <summary> System.Text.Json JsonDocument class aspect </summary>
[AspectClass("System.Text.Json", AspectType.Source)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class JsonDocumentAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JsonDocumentAspects>();

    /// <summary>
    /// Parse method aspect
    /// Taint all Parent from JsonElement that are string
    /// </summary>
    /// <param name="json">the JsonDocument result of Parse</param>
    /// <param name="options">the JsonDocumentOptions</param>
    /// <returns>the JsonDocument result</returns>
    [AspectMethodReplaceFromVersion("2.49.0", "System.Text.Json.JsonDocument::Parse(System.String,System.Text.Json.JsonDocumentOptions)")]
    public static object Parse(string json, JsonDocumentOptions options)
    {
        var doc = JsonDocument.Parse(json, options);

        try
        {
            TaintJsonElements(json, doc);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error tainting JsonDocument.Parse result");
        }

        return doc;
    }

    /// <summary>
    /// GetString method aspect
    /// Taint the string result when the parent is tainted
    /// </summary>
    /// <param name="target">the JsonElement instance</param>
    /// <returns>the string result</returns>
    [AspectMethodReplaceFromVersion("2.49.0", "System.Text.Json.JsonElement::GetString()", [0], [true])]
    public static string? GetString(object target)
#pragma warning disable DD0005 // Function is already safe where needed
    {
        IJsonElement? element;
        try
        {
            element = target.DuckCast<IJsonElement>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error casting to IJsonElement");
            return null;
        }

        var str = element.GetString();

        try
        {
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            var taintedTarget = taintedObjects?.Get(element.Parent);
            if (taintedObjects is not null && taintedTarget is not null)
            {
                taintedObjects.Taint(str, [new Range(0, str.Length)]);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error tainting JsonElement.GetString result");
        }

        return str;
    }
#pragma warning restore DD0005

    /// <summary>
    /// GetRawText method aspect
    /// Taint the raw string result when the parent is tainted
    /// </summary>
    /// <param name="target">the JsonElement instance</param>
    /// <returns>the raw string result</returns>
    [AspectMethodReplaceFromVersion("2.49.0", "System.Text.Json.JsonElement::GetRawText()", [0], [true])]
    public static string? GetRawText(object target)
#pragma warning disable DD0005  // Function is already safe where needed
    {
        IJsonElement? element;
        try
        {
            element = target.DuckCast<IJsonElement>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error casting to IJsonElement");
            return null;
        }

        var str = element.GetRawText();

        try
        {
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            var taintedTarget = taintedObjects?.Get(element.Parent);
            if (taintedObjects is not null && taintedTarget is not null)
            {
                taintedObjects.Taint(str, [new Range(0, str.Length)]);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error tainting JsonElement.GetRawText result");
        }

        return str;
    }
#pragma warning restore DD0005

    private static void TaintJsonElements(string json, JsonDocument doc)
    {
        var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
        var taintedTarget = taintedObjects?.Get(json);
        if (taintedObjects is null || taintedTarget is null)
        {
            return;
        }

        RecursiveTaintStringParents(doc.RootElement, taintedObjects);
    }

    private static void RecursiveTaintStringParents(JsonElement element, TaintedObjects map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    RecursiveTaintStringParents(property.Value, map);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    RecursiveTaintStringParents(item, map);
                }

                break;
            case JsonValueKind.String:
                var duckedElement = element.DuckCast<IJsonElement>();
                map.Taint(duckedElement.Parent, [new Range(0, 0)]);
                break;
        }
    }
}
#endif
