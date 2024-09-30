// <copyright file="DataStreamsContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Text;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Used for injecting the data streams pipeline context into headers
/// </summary>
internal class DataStreamsContextPropagator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsContextPropagator>();

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

        var encodedBytes = PathwayContextEncoder.Encode(context);
        var base64EncodedContext = Convert.ToBase64String(encodedBytes);
        headers.Add(DataStreamsPropagationHeaders.PropagationKeyBase64, Encoding.UTF8.GetBytes(base64EncodedContext));

        if (Tracer.Instance.Settings.IsDataStreamsLegacyHeadersEnabled)
        {
            headers.Add(DataStreamsPropagationHeaders.PropagationKey, encodedBytes);
        }
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

        // Try to extract from the base64 header first
        var base64Bytes = headers.TryGetLastBytes(DataStreamsPropagationHeaders.PropagationKeyBase64);
        if (base64Bytes is { Length: > 0 })
        {
            try
            {
                var base64String = Encoding.UTF8.GetString(base64Bytes);
                var decodedBytes = Convert.FromBase64String(base64String);
                return PathwayContextEncoder.Decode(decodedBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to decode base64 Data Streams context.");
                // Do not return null yet; try to extract from binary header
            }
        }

        // Fallback to the binary header if legacy headers are enabled
        if (Tracer.Instance.Settings.IsDataStreamsLegacyHeadersEnabled)
        {
            var binaryBytes = headers.TryGetLastBytes(DataStreamsPropagationHeaders.PropagationKey);
            if (binaryBytes is { Length: > 0 })
            {
                try
                {
                    return PathwayContextEncoder.Decode(binaryBytes);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to decode binary Data Streams context.");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
    /// This locks the sampling priority for <paramref name="context"/>.
    /// </summary>
    /// <param name="context">A <see cref="PathwayContext"/> value that will be propagated into <paramref name="headers"/>.</param>
    /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    public void InjectAsBase64String<TCarrier>(PathwayContext context, TCarrier headers)
        where TCarrier : IHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        headers.Add(DataStreamsPropagationHeaders.PropagationKeyBase64, Convert.ToBase64String(PathwayContextEncoder.Encode(context)));
    }

    /// <summary>
    /// Extracts a <see cref="PathwayContext"/> from the values found in the specified headers.
    /// </summary>
    /// <param name="headers">The headers that contain the values to be extracted.</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    /// <returns>A new <see cref="PathwayContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
    public PathwayContext? ExtractAsBase64String<TCarrier>(TCarrier headers)
        where TCarrier : IHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        var headerValues = headers.GetValues(DataStreamsPropagationHeaders.PropagationKeyBase64);
        if (headerValues is string[] stringValues)
        {
            // Checking string[] allows to avoid the enumerator allocation.
            foreach (string? headerValue in stringValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return PathwayContextEncoder.Decode(Convert.FromBase64String(headerValue));
                }
            }
        }
        else
        {
            // can happen if the value is coming from a user-provided getter, for instance via SpanContextExtractor
            foreach (var headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return PathwayContextEncoder.Decode(Convert.FromBase64String(headerValue));
                }
            }
        }

        return null;
    }
}
