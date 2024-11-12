// <copyright file="HttpCookieAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETFRAMEWORK

#nullable enable

using System;
using System.Web;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

// Every time that HttpCookie.Value is called in net framework, a new string is generated because
// it calls _multiValue.ToString(), which builds a new string from a new stringbuilder,
// so we need to taint the result of all these calls.

namespace Datadog.Trace.Iast.Aspects.System.Web;

/// <summary> HttpCookieAspect class aspects </summary>
[AspectClass("System.Web")]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpCookieAspect
{
    /// <summary> HttpCookie get of property value </summary>
    /// <param name="cookie"> The cookie </param>
    /// <returns> The value </returns>
    [AspectMethodReplace($"System.Web.HttpCookie::get_Value()")]
    public static string GetValue(HttpCookie cookie)
    {
        var value = cookie.Value;
        try
        {
            if (!string.IsNullOrEmpty(value))
            {
                PropagationModuleImpl.AddTaintedSource(value, new Source(SourceType.CookieValue, cookie?.Name, value));
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpCookieAspect)}.{nameof(GetValue)}");
        }

        return value;
    }
}
#endif
