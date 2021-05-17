// <copyright file="IHttpRouteData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.Integrations.AspNet
{
    /// <summary>
    /// IHttpRouteData interface for ducktyping
    /// </summary>
    public interface IHttpRouteData
    {
        IHttpRoute Route { get; }

        IDictionary<string, object> Values { get; }
    }
}
#endif
