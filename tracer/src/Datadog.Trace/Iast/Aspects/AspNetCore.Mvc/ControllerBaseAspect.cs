// <copyright file="ControllerBaseAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.AspNetCore.Mvc;

/// <summary> ControllerBaseAspect class aspect </summary>
[AspectClass("Microsoft.AspNetCore.Mvc", AspectType.Sink, VulnerabilityType.UnvalidatedRedirect)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class ControllerBaseAspect
{
    /// <summary>
    /// Redirect aspect
    /// </summary>
    /// <param name="url"> the target url </param>
    /// <returns> The target url </returns>
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::Redirect(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPermanent(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPreserveMethod(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::RedirectPermanentPreserveMethod(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirect(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPermanent(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPreserveMethod(System.String)")]
    [AspectMethodInsertBefore("Microsoft.AspNetCore.Mvc.ControllerBase::LocalRedirectPermanentPreserveMethod(System.String)")]
    public static string? Redirect(string? url)
    {
        try
        {
            return IastModule.OnUnvalidatedRedirect(url);
        }
        catch (global::System.Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(ControllerBaseAspect)}.{nameof(Redirect)}");
            return url;
        }
    }
}
