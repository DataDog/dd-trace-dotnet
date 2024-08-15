// <copyright file="HttpUtilityAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Propagation;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> WebClient class aspects </summary>
[AspectClass("System.Web;System.Runtime.Extensions;System.Web.HttpUtility", AspectType.Sink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpUtilityAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Web.HttpUtility::HtmlEncode(System.String)")]
    public static string? Review(string? parameter)
    {
        var result = WebUtility.HtmlEncode(parameter);
        try
        {
            if (parameter is not null && result is not null)
            {
                return IastModule.OnXssEscape(parameter, result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpUtilityAspect)}.{nameof(Review)}");
        }

        return result;
    }
}
