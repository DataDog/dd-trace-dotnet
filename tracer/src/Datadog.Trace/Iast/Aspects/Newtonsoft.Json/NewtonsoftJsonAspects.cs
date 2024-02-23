// <copyright file="NewtonsoftJsonAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.Newtonsoft.Json;

/// <summary> Newtonsoft.Json class aspects </summary>
[AspectClass("Newtonsoft.Json")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class NewtonsoftJsonAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NewtonsoftJsonAspects>();

    private static readonly Type JObjectType = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json")!;
    private static readonly Type JArrayType = Type.GetType("Newtonsoft.Json.Linq.JArray, Newtonsoft.Json")!;
    private static readonly Type JTokenType = Type.GetType("Newtonsoft.Json.Linq.JToken, Newtonsoft.Json")!;

    /// <summary>
    /// JObject Parse aspect.
    /// </summary>
    /// <param name="json">The parsed Json string.</param>
    /// <returns>The parsed JObject instance created.</returns>
    [AspectMethodReplace("Newtonsoft.Json.Linq.JObject::Parse(System.String)")]
    public static object? ParseObject(string json)
    {
        var proxyResult = DuckType.GetOrCreateProxyType(typeof(ICanParse), JObjectType);
        object? result;
        if (proxyResult.Success)
        {
            var proxy = (ICanParse)proxyResult.CreateInstance(null!);
            result = proxy.Parse(json);
        }
        else
        {
            Log.Warning("Failed to create JObject proxy");
            return null;
        }

        try
        {
            var duckedResult = result.DuckCast<IJObject>();
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            RecursiveJObjectStringTaint(duckedResult, taintedObjects);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while tainting the JObject");
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
        var proxyResult = DuckType.GetOrCreateProxyType(typeof(ICanParse), JArrayType);
        object? result;
        if (proxyResult.Success)
        {
            var proxy = (ICanParse)proxyResult.CreateInstance(null!);
            result = proxy.Parse(json);
        }
        else
        {
            Log.Warning("Failed to create JArray proxy");
            return null;
        }

        try
        {
            if (result is null)
            {
                return null;
            }

            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            RecursiveJArrayStringTaint((IEnumerable)result, taintedObjects);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while tainting the JArray");
        }

        return result;
    }

    /// <summary>
    /// JToken Parse aspect.
    /// </summary>
    /// <param name="json">The parsed Json string.</param>
    /// <returns>The parsed JToken instance created.</returns>
    [AspectMethodReplace("Newtonsoft.Json.Linq.JToken::Parse(System.String)")]
    public static object? ParseToken(string json)
    {
        var proxyResult = DuckType.GetOrCreateProxyType(typeof(ICanParse), JTokenType);
        object? result;
        if (proxyResult.Success)
        {
            var proxy = (ICanParse)proxyResult.CreateInstance(null!);
            result = proxy.Parse(json);
        }
        else
        {
            Log.Warning("Failed to create JToken proxy");
            return null;
        }

        try
        {
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            RecursiveJTokenStringTaint(result, taintedObjects);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while tainting the JToken");
        }

        return result;
    }

    private static void RecursiveJTokenStringTaint(object? token, TaintedObjects? taintedObjects)
    {
        if (token == null || taintedObjects == null)
        {
            return;
        }

        if (!token.TryDuckCast<IJToken>(out var jToken))
        {
            return;
        }

        switch (jToken.Type)
        {
            case JTokenTypeProxy.Object:
                RecursiveJObjectStringTaint(token.DuckCast<IJObject>(), taintedObjects);
                break;

            case JTokenTypeProxy.Array:
                RecursiveJArrayStringTaint((IEnumerable)token, taintedObjects);
                break;

            case JTokenTypeProxy.String:
                RecursiveJValueStringTaint(token.DuckCast<IJValue>(), taintedObjects);
                break;
        }
    }

    private static void RecursiveJArrayStringTaint(IEnumerable? array, TaintedObjects? taintedObjects)
    {
        if (array == null || taintedObjects == null)
        {
            return;
        }

        foreach (var value in array)
        {
            RecursiveJTokenStringTaint(value, taintedObjects);
        }
    }

    private static void RecursiveJObjectStringTaint(IJObject? obj, TaintedObjects? taintedObjects)
    {
        if (obj == null || taintedObjects == null)
        {
            return;
        }

        foreach (var value in obj.Properties())
        {
            if (value == null)
            {
                continue;
            }

            var duckedProperty = value.DuckCast<JPropertyStruct>();
            RecursiveJTokenStringTaint(duckedProperty.Value, taintedObjects);
        }
    }

    private static void RecursiveJValueStringTaint(IJValue? value, TaintedObjects? taintedObjects)
    {
        if (value == null || taintedObjects == null)
        {
            return;
        }

        if (value is { Type: JTokenTypeProxy.String, Value: string str })
        {
            taintedObjects.Taint(str, [new Range(0, str.Length)]);
        }
    }
}
