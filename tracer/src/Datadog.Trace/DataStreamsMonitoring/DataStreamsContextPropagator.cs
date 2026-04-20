// <copyright file="DataStreamsContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.DataStreamsMonitoring;

/// <summary>
/// Used for injecting the data streams pipeline context into headers
/// </summary>
internal sealed class DataStreamsContextPropagator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DataStreamsContextPropagator>();

    public static DataStreamsContextPropagator Instance { get; } = new();

    /// <summary>
    /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
    /// This locks the sampling priority for <paramref name="context"/>.
    /// </summary>
    /// <param name="context">A <see cref="PathwayContext"/> value that will be propagated into <paramref name="headers"/>.</param>
    /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
    /// <param name="isDataStreamsLegacyHeadersEnabled">Are legacy DSM headers enabled</param>
    /// <typeparam name="TCarrier">Type of header collection</typeparam>
    internal void Inject<TCarrier>(PathwayContext context, TCarrier headers, bool isDataStreamsLegacyHeadersEnabled)
        where TCarrier : IBinaryHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        var encodedBytes = PathwayContextEncoder.Encode(context);

        // Calculate the maximum length of the base64 encoded data
        // Base64 encoding encodes 3 bytes of data into 4 bytes of encoded data
        // So the maximum length is ceil(encodedBytes.Length / 3) * 4 and using integer arithmetic it's ((encodedBytes.Length + 2) / 3) * 4
        int base64Length = ((encodedBytes.Length + 2) / 3) * 4;
        byte[] base64EncodedContextBytes = new byte[base64Length];
        var status = Base64.EncodeToUtf8(encodedBytes, base64EncodedContextBytes, out _, out int bytesWritten);

        if (status != OperationStatus.Done)
        {
            Log.Error("Failed to encode Data Streams context to Base64. OperationStatus: {Status}", status);
            return;
        }

        if (bytesWritten == base64EncodedContextBytes.Length)
        {
            headers.Add(DataStreamsPropagationHeaders.PropagationKeyBase64, base64EncodedContextBytes);
        }
        else
        {
            headers.Add(DataStreamsPropagationHeaders.PropagationKeyBase64, base64EncodedContextBytes.AsSpan(0, bytesWritten).ToArray());
        }

        if (isDataStreamsLegacyHeadersEnabled)
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
        => Extract(headers, Tracer.Instance.Settings.IsDataStreamsLegacyHeadersEnabled);

    [TestingAndPrivateOnly]
    internal PathwayContext? Extract<TCarrier>(TCarrier headers, bool isDataStreamsLegacyHeadersEnabled)
        where TCarrier : IBinaryHeadersCollection
    {
        if (headers is null) { ThrowHelper.ThrowArgumentNullException(nameof(headers)); }

        // Try to extract from the base64 header first
        var base64Bytes = headers.TryGetLastBytes(DataStreamsPropagationHeaders.PropagationKeyBase64);
        if (base64Bytes is { Length: > 0 })
        {
            try
            {
                // Calculate the maximum decoded length
                // Base64 encoding encodes 3 bytes of data into 4 bytes of encoded data
                // So the maximum decoded length is (base64Bytes.Length * 3) / 4
                int decodedLength = (base64Bytes.Length * 3) / 4;
                byte[] decodedBytes = new byte[decodedLength];

                var status = Base64.DecodeFromUtf8(base64Bytes, decodedBytes, out _, out int bytesWritten);

                if (status != OperationStatus.Done)
                {
                    Log.Error("Failed to decode Base64 data streams context. OperationStatus: {Status}", status);
                    return null;
                }
                else
                {
                    if (bytesWritten == decodedBytes.Length)
                    {
                        return PathwayContextEncoder.Decode(decodedBytes);
                    }
                    else
                    {
                        return PathwayContextEncoder.Decode(decodedBytes.AsSpan(0, bytesWritten).ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to decode base64 Data Streams context.");
            }
        }

        if (isDataStreamsLegacyHeadersEnabled)
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
