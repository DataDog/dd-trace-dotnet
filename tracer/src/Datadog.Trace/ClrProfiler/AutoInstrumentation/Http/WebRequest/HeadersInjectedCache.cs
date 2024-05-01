// <copyright file="HeadersInjectedCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Net;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.WebRequest;

internal static class HeadersInjectedCache
{
    private static readonly object InjectedValue = new();
    private static readonly ConditionalWeakTable<WebHeaderCollection, object> Cache = new();

    public static void SetInjectedHeaders(WebHeaderCollection headers)
    {
#if NETCOREAPP3_1_OR_GREATER
        Cache.AddOrUpdate(headers, InjectedValue);
#else
        Cache.GetValue(headers, _ => InjectedValue);
#endif
    }

    public static bool TryGetInjectedHeaders(WebHeaderCollection headers)
    {
        return Cache.TryGetValue(headers, out _);
    }
}
