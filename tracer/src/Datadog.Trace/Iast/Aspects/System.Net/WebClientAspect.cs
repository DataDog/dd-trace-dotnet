// <copyright file="WebClientAspect.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.AppSec;
using Datadog.Trace.Iast.Dataflow;

#nullable enable

namespace Datadog.Trace.Iast.Aspects.System.Net;

/// <summary> WebClient class aspects </summary>
[AspectClass("System.Net.WebClient,System", AspectType.RaspIastSink, VulnerabilityType.Ssrf)]
[global::System.ComponentModel.Browsable(false)]
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public class WebClientAspect
{
    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadData(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataTaskAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFile(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileTaskAsync(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadString(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringTaskAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenRead(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenReadTaskAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.String)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.String,System.Byte[])", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.String,System.String,System.Byte[])", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.String,System.Byte[])", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.String,System.String,System.Byte[])", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.String,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.String,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.String,System.Collections.Specialized.NameValueCollection)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.String,System.String,System.Collections.Specialized.NameValueCollection)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.String,System.Collections.Specialized.NameValueCollection)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.String,System.String,System.Collections.Specialized.NameValueCollection)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::set_BaseAddress(System.String)")]
    public static string Review(string parameter)
    {
        try
        {
            if (parameter is not null)
            {
                VulnerabilitiesModule.OnSSRF(parameter);
            }

            return parameter!;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebClientAspect)}.{nameof(Review)}");
            return parameter;
        }
    }

    /// <summary>
    /// Launches a SSRF vulnerability if the url is tainted
    /// </summary>
    /// <param name="parameter">the sensitive parameter of the method</param>
    /// <returns>the parameter</returns>
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadData(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataAsync(System.Uri,System.Object)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataTaskAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFile(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileAsync(System.Uri,System.String,System.Object)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileTaskAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadString(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringAsync(System.Uri,System.Object)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringTaskAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenRead(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenReadAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenReadAsync(System.Uri,System.Object)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenReadTaskAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri,System.String,System.Object)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.Uri)")]
    [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.Uri,System.Byte[])", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.Uri,System.String,System.Byte[])", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.Byte[])", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.String,System.Byte[])", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.String,System.Byte[],System.Object)", 3)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.Uri,System.Byte[])", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.Uri,System.String,System.Byte[])", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String,System.String,System.Object)", 3)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String,System.String,System.Object)", 3)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.Uri,System.String)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.Uri,System.String,System.String)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection,System.Object)", 3)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]
    [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]
    public static Uri ReviewUri(Uri parameter)
    {
        try
        {
            if (parameter is not null && parameter.OriginalString is not null)
            {
                VulnerabilitiesModule.OnSSRF(parameter.OriginalString);
            }

            return parameter!;
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            IastModule.Log.Error(ex, $"Error invoking {nameof(WebClientAspect)}.{nameof(ReviewUri)}");
            return parameter;
        }
    }
}
