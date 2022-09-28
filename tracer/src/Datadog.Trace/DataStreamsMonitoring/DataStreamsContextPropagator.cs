// <copyright file="DataStreamsContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Headers;
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
        where TCarrier : IBinaryHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        var bytes = headers.TryGetBytes(PropagationKey);

        return bytes is { } ? PathwayContextEncoder.Decode(bytes) : null;
    }
}
