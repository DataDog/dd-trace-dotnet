// <copyright file="JavaScriptSerializerAspects.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using Datadog.Trace.Iast.Dataflow;

namespace Datadog.Trace.Iast.Aspects.System.Web;

/// <summary> JavaScriptSerializer class aspect </summary>
[AspectClass("System.Web.Extensions")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class JavaScriptSerializerAspects
{
    /// <summary>
    /// DeserializeObject aspect
    /// </summary>
    /// <param name="input"> the json string </param>
    /// <returns> The target url </returns>
    [AspectMethodReplace("System.Web.Script.Serialization.JavaScriptSerializer::DeserializeObject(System.String)")]
    public static object DeserializeObject(string input)
    {
        return System.Web.Script.Serialization.JavaScriptSerializer.DeserializeObject(input);
    }
}

#endif
