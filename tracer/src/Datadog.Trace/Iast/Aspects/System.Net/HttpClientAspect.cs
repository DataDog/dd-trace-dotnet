// <copyright file="HttpClientAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Net.Http;
#endif
using System;
using Datadog.Trace.AppSec;

#if NETFRAMEWORK
using Datadog.Trace.DuckTyping;
#endif
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> HttpClient class aspects </summary>
[AspectClass("System.Net.Http", AspectType.RaspIastSink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class HttpClientAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url string is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.String)")]
#if NETCOREAPP3_1_OR_GREATER
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.String,System.Threading.CancellationToken)", 1)]
#endif
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken) ", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.String,System.Net.Http.HttpContent) ", 1)]
#endif
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Net.Http.HttpCompletionOption)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.String,System.Net.Http.HttpContent)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.String,System.Net.Http.HttpContent)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.String,System.Threading.CancellationToken)", 1)]
    public static string Review(string parameter)
    {
        try
        {
            VulnerabilitiesModule.OnSSRF(parameter);
            return parameter;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpClientAspect)}.{nameof(Review)}");
            return parameter;
        }
    }

    /// <summary>
    /// Launches a SSRF vulnerability if the uri is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.Uri)")]
#if NETCOREAPP3_1_OR_GREATER
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.Uri,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.Uri,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.Uri,System.Threading.CancellationToken)", 1)]
#endif
#if !NETFRAMEWORK
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.Uri,System.Net.Http.HttpContent)", 1)]
#endif
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Net.Http.HttpCompletionOption)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.Uri,System.Net.Http.HttpContent)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.Uri,System.Net.Http.HttpContent)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.Uri,System.Threading.CancellationToken)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::set_BaseAddress(System.Uri)")]
    public static Uri ReviewUri(Uri parameter)
    {
        try
        {
            if (parameter is not null)
            {
                VulnerabilitiesModule.OnSSRF(parameter.OriginalString);
            }

            return parameter!;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpClientAspect)}.{nameof(ReviewUri)}");
            return parameter;
        }
    }

    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]
#if NETCOREAPP3_1_OR_GREATER
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage)")]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption)", 1)]
    [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]
    [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::Send(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]
#endif
    [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]
#if !NETFRAMEWORK
    public static HttpRequestMessage ReviewHttpRequestMessage(HttpRequestMessage parameter)
    {
        try
        {
            if (parameter is not null && parameter.RequestUri is not null && parameter.RequestUri.OriginalString is not null)
            {
                VulnerabilitiesModule.OnSSRF(parameter.RequestUri.OriginalString);
            }

            return parameter!;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpClientAspect)}.{nameof(ReviewHttpRequestMessage)}");
            return parameter;
        }
    }
#else
    public static object ReviewHttpRequestMessage(object parameter)
    {
        try
        {
            if (parameter is not null)
            {
                var uri = parameter.DuckCast<ClrProfiler.AutoInstrumentation.AspNet.IHttpRequestMessage>()?.RequestUri;

                if (uri is not null)
                {
                    VulnerabilitiesModule.OnSSRF(uri.OriginalString);
                }
            }

            return parameter!;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(HttpClientAspect)}.{nameof(ReviewHttpRequestMessage)}");
            return parameter;
        }
    }
#endif
}
