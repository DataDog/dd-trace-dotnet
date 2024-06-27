// <copyright file="HttpTransportBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif

namespace Datadog.Trace.AppSec.Coordinator;

internal abstract class HttpTransportBase
{
    private bool _isAdditiveContextDisposed;

    internal abstract bool IsBlocked { get; }

    internal abstract int StatusCode { get; }

    internal abstract IDictionary<string, object>? RouteData { get; }

    internal abstract bool ReportedExternalWafsRequestHeaders { get; set; }

    public abstract HttpContext Context { get; }

    internal abstract IContext? GetAdditiveContext();

    /// <summary>
    /// Disposes the WAF's context stored in HttpContext.Items[]. If it doesn't exist, nothing happens, no crash
    /// </summary>
    internal void DisposeAdditiveContext()
    {
        GetAdditiveContext()?.Dispose();
        _isAdditiveContextDisposed = true;
    }

    internal bool IsAdditiveContextDisposed() => _isAdditiveContextDisposed;

    internal abstract void SetAdditiveContext(IContext additiveContext);

    internal abstract IHeadersCollection GetRequestHeaders();

    internal abstract IHeadersCollection GetResponseHeaders();

    internal abstract void MarkBlocked();
}
