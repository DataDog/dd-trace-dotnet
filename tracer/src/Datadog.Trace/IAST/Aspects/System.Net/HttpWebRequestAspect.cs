// <copyright file="HttpWebRequestAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> HttpClient class aspects </summary>
[AspectClass("System.Net.Requests,System", AspectType.Sink, VulnerabilityType.SSRF)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpWebRequestAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateDefault(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.String)")]
    public static object Review(object parameter)
    {
        IastModule.OnSSRF(parameter);
        return parameter;
    }
}
