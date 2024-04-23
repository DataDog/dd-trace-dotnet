// <copyright file="PushStreamContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.Trace.HttpOverStreams.HttpContent;

/// <summary>
/// Provides an <see cref="HttpContent"/> implementation that exposes an output <see cref="Stream"/>
/// which can be written to directly. The ability to push data to the output stream differs from the
/// StreamContent where data is pulled and not pushed.
/// </summary>
internal class PushStreamContent : IHttpContent
{
    private readonly Func<Stream, Task> _onStreamAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushStreamContent"/> class.
    /// </summary>
    /// <param name="onStreamAvailable">The action to call when an output stream is available. When the
    /// output stream is closed or disposed, it will signal to the content that it has completed and the
    /// HTTP request or response will be completed.</param>
    public PushStreamContent(Func<Stream, Task> onStreamAvailable)
    {
        _onStreamAvailable = onStreamAvailable;
    }

    // We don't know the length, so don't send a content-length header
    public long? Length => null;

    public async Task CopyToAsync(Stream destination)
    {
        // Note the callee must not close or dispose the stream because they don't own it
        // We _could_ use a wrapper stream to enforce that, but then it _requires_ the
        // callee to call close/dispose which seems weird

        // We're doing chunked encoding, so we need to wrap the stream to ensure we add the required chunked encoding headers
        using var wrappedStream = new ChunkedEncodingWriteStream(destination);
        await _onStreamAvailable(wrappedStream).ConfigureAwait(false);
        await wrappedStream.FinishAsync().ConfigureAwait(false); // write the final block
    }

    public Task CopyToAsync(byte[] buffer)
    {
        // This CopyToAsync overload is only used to read responses
        // And we never use PushStreamContent for that
        throw new NotImplementedException();
    }
}
