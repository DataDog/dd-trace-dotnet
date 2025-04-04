// <copyright file="HeadersCollectionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Headers;

internal static class HeadersCollectionExtensions
{
    /// <summary>
    /// Helper method that returns an accessor for the given <typeparamref name="TCarrier"/> type.
    /// Note that the value of <paramref name="carrier"/> is not used.
    /// </summary>
    public static HeadersCollectionAccesor<TCarrier> GetAccessor<TCarrier>(this TCarrier? carrier)
        where TCarrier : IHeadersCollection
    {
        return new HeadersCollectionAccesor<TCarrier>();
    }
}
