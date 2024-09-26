// <copyright file="HttpUtilityAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> WebClient class aspects </summary>
[AspectClass("System.Web;System.Runtime.Extensions;System.Web.HttpUtility", AspectType.Sink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpUtilityAspect
{
    /// <summary>
    /// Escapes the HTML string making it safe for XSS
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Web.HttpUtility::HtmlEncode(System.String)")]
    public static string? XssEscape(string? parameter)
    {
        var result = global::System.Web.HttpUtility.HtmlEncode(parameter);
        try
        {
            if (parameter is not null && result is not null)
            {
                return IastModule.OnXssEscape(parameter, result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpUtilityAspect)}.{nameof(XssEscape)}");
        }

        return result;
    }

    /// <summary>
    /// Escapes the URL string making it safe for SSRF
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Web.HttpUtility::UrlEncode(System.String)")]
    public static string? SsrfEscape(string? parameter)
    {
        var result = global::System.Web.HttpUtility.UrlEncode(parameter);
        try
        {
            if (parameter is not null && result is not null)
            {
                return IastModule.OnSsrfEscape(parameter, result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpUtilityAspect)}.{nameof(SsrfEscape)}");
        }

        return result;
    }

    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <param name="e">encoding used to encode the string</param>
    /// <returns>the parameter</returns>
    [AspectMethodReplace("System.Web.HttpUtility::UrlEncode(System.String,System.Text.Encoding)")]
    public static string? SsrfEscape(string? parameter, global::System.Text.Encoding e)
    {
        var result = global::System.Web.HttpUtility.UrlEncode(parameter, e);
        try
        {
            if (parameter is not null && result is not null)
            {
                return IastModule.OnSsrfEscape(parameter, result);
            }
        }
        catch (Exception ex)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpUtilityAspect)}.{nameof(SsrfEscape)}");
        }

        return result;
    }
}
