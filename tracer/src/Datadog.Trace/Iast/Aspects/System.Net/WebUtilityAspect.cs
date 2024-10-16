// <copyright file="WebUtilityAspect.cs" company="Datadog">
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
[AspectClass("System.Private.Corelib;System.Runtime", AspectType.Sink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class WebUtilityAspect
{
    /// <summary>
    /// Escapes the HTML string making it safe for XSS
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Net.WebUtility::HtmlEncode(System.String)")]
    public static string? XssEscape(string? parameter)
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
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebUtilityAspect)}.{nameof(XssEscape)}");
        }

        return result;
    }

    /// <summary>
    /// Escapes the URL string making it safe for SSRF
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Net.WebUtility::UrlEncode(System.String)")]
    public static string? SsrfEscape(string? parameter)
    {
        var result = WebUtility.UrlEncode(parameter);
        try
        {
            if (parameter is not null && result is not null)
            {
                return IastModule.OnSsrfEscape(parameter, result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebUtilityAspect)}.{nameof(SsrfEscape)}");
        }

        return result;
    }
}
