// <copyright file="HttpResponseAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.AspNetCore.Http;

/// <summary> HttpResponseAspect class aspect </summary>
[AspectClass("Microsoft.AspNetCore.Http", AspectType.Sink, VulnerabilityType.UnvalidatedRedirect)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpResponseAspect
{
    /// <summary>
    /// Redirect aspect
    /// </summary>
    /// <param name="url"> the target url </param>
    /// <returns> The target url </returns>
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Http.HttpResponse::Redirect(System.String,System.Boolean)", 1)]
    public static string? Redirect(string? url)
    {
        try
        {
            return IastModule.OnUnvalidatedRedirect(url);
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpResponseAspect)}.{nameof(Redirect)}");
            return url;
        }
    }
}
