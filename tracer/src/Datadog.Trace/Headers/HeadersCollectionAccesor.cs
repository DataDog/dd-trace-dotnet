// <copyright file="HeadersCollectionAccesor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.Headers;

internal readonly struct HeadersCollectionAccesor<TCarrier> : ICarrierGetter<TCarrier>, ICarrierSetter<TCarrier>
    where TCarrier : IHeadersCollection
{
    public IEnumerable<string?> Get(TCarrier carrier, string key)
    {
        return carrier.GetValues(key);
    }

    public void Set(TCarrier carrier, string key, string value)
    {
        carrier.Set(key, value);
    }
}
