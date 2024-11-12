// <copyright file="JavaScriptSerializerAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Iast.Aspects.System.Web.Extensions;

/// <summary> JavaScriptSerializer class aspect </summary>
[AspectClass("System.Web.Extensions")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class JavaScriptSerializerAspects
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<JavaScriptSerializerAspects>();

    /// <summary>
    /// DeserializeObject aspect
    /// </summary>
    /// <param name="instance"> The instance </param>
    /// <param name="input"> the json string </param>
    /// <returns> The target url </returns>
    [AspectMethodReplace("System.Web.Script.Serialization.JavaScriptSerializer::DeserializeObject(System.String)")]
    public static object? DeserializeObject(object instance, string input)
#pragma warning disable DD0005
    {
        IJavaScriptSerializer? serializer;
        try
        {
            serializer = instance.DuckCast<IJavaScriptSerializer>();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while casting JavaScriptSerializer");
            return null;
        }

        var result = serializer.DeserializeObject(input);

        try
        {
            var taintedObjects = IastModule.GetIastContext()?.GetTaintedObjects();
            var taintedTarget = taintedObjects?.Get(input);
            if (taintedObjects is null || taintedTarget is null)
            {
                return result;
            }

            TaintObject(result, taintedObjects);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while tainting json in DeserializeObject");
        }

        return result;
    }
#pragma warning restore DD0005

    private static void TaintObject(object obj, TaintedObjects taintedObjects)
    {
        switch (obj)
        {
            case string str:
                taintedObjects.Taint(obj, [new Range(0, str.Length)]);
                break;

            case Dictionary<string, object> objects:
                foreach (var item in objects)
                {
                    TaintObject(item.Value, taintedObjects);
                }

                break;

            case object[] objects:
                foreach (var item in objects)
                {
                    TaintObject(item, taintedObjects);
                }

                break;
        }
    }
}

#endif
