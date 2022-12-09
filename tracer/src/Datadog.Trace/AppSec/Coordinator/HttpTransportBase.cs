// <copyright file="HttpTransportBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Headers;

namespace Datadog.Trace.AppSec.Coordinator;

internal abstract class HttpTransportBase
{
    internal abstract bool IsBlocked { get; }

    internal abstract IContext GetAdditiveContext();

    internal void DisposeAdditiveContext() => GetAdditiveContext()?.Dispose();

    internal abstract void SetAdditiveContext(IContext additiveContext);

    internal abstract IHeadersCollection GetRequestHeaders();

    internal abstract IHeadersCollection GetResponseHeaders();

    internal abstract void MarkBlocked();
}
