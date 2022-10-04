// <copyright file="DataStreamsContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Used for injecting the data streams pipeline context into headers
/// </summary>
internal class DataStreamsContextPropagator
{
    private const string PropagationKey = "dd-pathway-ctx";

    public static DataStreamsContextPropagator Instance { get; } = new();

    /// <summary>
    /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
    /// This locks the sampling priority for <paramref name="context"/>.
    /// </summary>
    /// <param name="context">A <see cref="PathwayContext"/> value that will be propagated into <paramref name="headers"/>.</param>
    /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    public void Inject<TCarrier>(PathwayContext context, TCarrier headers)
        where TCarrier : IBinaryHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        headers.Add(PropagationKey, PathwayContextEncoder.Encode(context));
    }

    /// <summary>
    /// Extracts a <see cref="PathwayContext"/> from the values found in the specified headers.
    /// </summary>
    /// <param name="headers">The headers that contain the values to be extracted.</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    /// <returns>A new <see cref="PathwayContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
    public PathwayContext? Extract<TCarrier>(TCarrier headers)
        where TCarrier : IBinaryHeadersCollection =>
        Extract(headers, default(HeadersCollectionGetterAndSetter<TCarrier>));

    /// <summary>
    /// Extracts a <see cref="PathwayContext"/> from the values found in the specified headers.
    /// </summary>
    /// <param name="headers">The headers that contain the values to be extracted.</param>
    /// <param name="getter">The function that can extract the last byte[] for a given header name
    /// (or null if the name does not exist).</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    /// <returns>A new <see cref="PathwayContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
    public PathwayContext? Extract<TCarrier>(TCarrier headers, Func<TCarrier, string, byte[]?> getter)
    {
        if (getter == null) { ThrowHelper.ThrowArgumentNullException(nameof(getter)); }

        return Extract(headers, new FuncGetter<TCarrier>(getter));
    }

    private PathwayContext? Extract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter)
        where TCarrierGetter : struct, IBinaryCarrierGetter<TCarrier>
    {
        if (carrier is null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }

        var bytes = carrierGetter.Get(carrier, PropagationKey);
        return bytes is { } ? PathwayContextEncoder.Decode(bytes) : null;
    }

    private readonly struct HeadersCollectionGetterAndSetter<TCarrier> : IBinaryCarrierGetter<TCarrier>, IBinaryCarrierSetter<TCarrier>
        where TCarrier : IBinaryHeadersCollection
    {
        public byte[]? Get(TCarrier carrier, string key)
        {
            return carrier.TryGetBytes(key);
        }

        public void Set(TCarrier carrier, string key, byte[] value)
        {
            carrier.Add(key, value);
        }
    }

    private readonly struct FuncGetter<TCarrier> : IBinaryCarrierGetter<TCarrier>
    {
        private readonly Func<TCarrier, string, byte[]?> _getter;

        public FuncGetter(Func<TCarrier, string, byte[]?> getter)
        {
            _getter = getter;
        }

        public byte[]? Get(TCarrier carrier, string key)
        {
            return _getter(carrier, key);
        }
    }

    private readonly struct ActionSetter<TCarrier> : IBinaryCarrierSetter<TCarrier>
    {
        private readonly Action<TCarrier, string, byte[]> _setter;

        public ActionSetter(Action<TCarrier, string, byte[]> setter)
        {
            _setter = setter;
        }

        public void Set(TCarrier carrier, string key, byte[] value)
        {
            _setter(carrier, key, value);
        }
    }
}
