// <copyright file="JObjectAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

/// <summary> uri class aspects </summary>
[AspectClass("Newtonsoft.Json")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class JObjectAspects
{
    /// <summary>
    /// JObject Parse aspect.
    /// </summary>
    /// <param name="json">The parsed Json string.</param>
    /// <returns>The parsed JObject instance created.</returns>
    [AspectMethodReplace("Newtonsoft.Json.Linq.JObject::Parse(System.String)")]
    public static object? ParseObject(string json)
    {
        var type = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json")!;
        var method = type.GetMethod("Parse", [typeof(string)]);
        var result = method?.Invoke(null, [json]);

        try
        {
            var duckedResult = result.DuckCast<IJObject>();
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            RecursiveJObjectStringTaint(duckedResult, taintedObjects);
        }
        catch (Exception ex)
        {
            // TODO: log
            Console.WriteLine(ex);
        }

        return result;
    }

    /// <summary>
    /// JArray Parse aspect.
    /// </summary>
    /// <param name="json">The parsed Json string.</param>
    /// <returns>The parsed JArray instance created.</returns>
    [AspectMethodReplace("Newtonsoft.Json.Linq.JArray::Parse(System.String)")]
    public static object? ParseArray(string json)
    {
        var type = Type.GetType("Newtonsoft.Json.Linq.JArray, Newtonsoft.Json")!;
        var method = type.GetMethod("Parse", [typeof(string)]);
        var result = method?.Invoke(null, [json]);

        try
        {
            var duckedResult = result.DuckCast<IJObject>();
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            RecursiveJObjectStringTaint(duckedResult, taintedObjects);
        }
        catch (Exception ex)
        {
            // TODO: log
            Console.WriteLine(ex);
        }

        return result;
    }

    private static void RecursiveJObjectStringTaint(IJObject? obj, TaintedObjects? taintedObjects)
    {
        if (obj == null || taintedObjects == null)
        {
            return;
        }

        foreach (var value in obj.Properties())
        {
            var duckedProperty = value.DuckCast<JPropertyStruct>();
            var token = duckedProperty.Value.DuckCast<IJToken>();
            switch (token.Type)
            {
                case JTokenTypeProxy.Object:
                    var dockedObject = duckedProperty.Value.DuckCast<IJObject>();
                    RecursiveJObjectStringTaint(dockedObject, taintedObjects);
                    break;
                case JTokenTypeProxy.Array:
                    var arrayValue = (object[])duckedProperty.Value.DuckCast<IJValue>().Value;
                    foreach (var item in arrayValue)
                    {
                        var duckedItem = item.DuckCast<IJObject>(); // JToken??
                        RecursiveJObjectStringTaint(duckedItem, taintedObjects);
                    }

                    break;
                case JTokenTypeProxy.String:
                    var propertyValue = duckedProperty.Value.DuckCast<IJValue>();
                    if (propertyValue.Value is string str)
                    {
                        taintedObjects.Taint(str, [new Range(0, str.Length)]);
                    }

                    break;
            }
        }
    }
}
