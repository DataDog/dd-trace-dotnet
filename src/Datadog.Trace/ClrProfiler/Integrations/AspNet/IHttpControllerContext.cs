// <copyright file="IHttpControllerContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

#if NETFRAMEWORK
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// HttpControllerContext interface for ducktyping
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IHttpControllerContext
    {
        IHttpRequestMessage Request { get; }

        IHttpRouteData RouteData { get; }
    }
}
#endif
