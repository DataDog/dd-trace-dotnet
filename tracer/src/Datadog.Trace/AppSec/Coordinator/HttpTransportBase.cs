// <copyright file="HttpTransportBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;

namespace Datadog.Trace.AppSec.Coordinator;

internal abstract class HttpTransportBase
{
    internal const string AsmApiSecurity = "asm.apisecurity";

    internal abstract bool IsBlocked { get; }

    internal abstract IContext GetAdditiveContext();

    /// <summary>
    /// Disposes the WAF's context stored in HttpContext.Items[]. If it doesn't exist, nothing happens, no crash
    /// </summary>
    internal void DisposeAdditiveContext() => GetAdditiveContext()?.Dispose();

    internal abstract void SetAdditiveContext(IContext additiveContext);

    internal abstract IHeadersCollection GetRequestHeaders();

    internal abstract IHeadersCollection GetResponseHeaders();

    internal abstract void MarkBlocked();
}
