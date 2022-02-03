// <copyright file="IAttributeRouteInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies
{
    internal interface IAttributeRouteInfo
    {
        string? Template { get; }
    }
}
#endif
