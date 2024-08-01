// <copyright file="WebRequestAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> HttpWebRequest class aspects </summary>
[AspectClass("System.Net.Requests,System,netstandard", AspectType.RaspIastSink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class WebRequestAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.String)")]
    public static object Review(string parameter)
    {
        try
        {
            VulnerabilitiesModule.OnSSRF(parameter);
            return parameter;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebRequestAspect)}.{nameof(Review)}");
            return parameter;
        }
    }

    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateDefault(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.Uri)")]
    public static object Review(Uri parameter)
    {
        try
        {
            VulnerabilitiesModule.OnSSRF(parameter.OriginalString);
            return parameter;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebRequestAspect)}.{nameof(Review)}");
            return parameter;
        }
    }
}
