// <copyright file="IHttpRoute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNet
{
    /// <summary>
    /// IHttpRoute proxy for ducktyping
    /// </summary>
    internal interface IHttpRoute
    {
        string RouteTemplate { get; }
    }
}
#endif
