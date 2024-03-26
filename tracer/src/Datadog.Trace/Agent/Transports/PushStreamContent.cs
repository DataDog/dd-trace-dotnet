// <copyright file="PushStreamContent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on code from https://github.com/aspnet/AspNetWebStack/blob/1231b77d79956152831b75ad7f094f844251b97f/src/System.Net.Http.Formatting/PushStreamContent.cs
// and https://github.com/aspnet/AspNetWebStack/blob/1231b77d79956152831b75ad7f094f844251b97f/src/System.Net.Http.Formatting/Internal/DelegatingStream.cs
// which is licensed as:
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

#if NETCOREAPP

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Datadog.Trace.Agent.Transports;

/// <summary>
/// Provides an <see cref="HttpContent"/> implementation that exposes an output <see cref="Stream"/>
/// which can be written to directly. The ability to push data to the output stream differs from the
/// <see cref="StreamContent"/> where data is pulled and not pushed.
/// </summary>
internal class PushStreamContent : HttpContent
{
    private readonly Func<Stream, Task> _onStreamAvailable;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushStreamContent"/> class with the given <see cref="MediaTypeHeaderValue"/>.
    /// </summary>
    /// <param name="onStreamAvailable">The action to call when an output stream is available. When the
    /// output stream is closed or disposed, it will signal to the content that it has completed and the
    /// HTTP request or response will be completed.</param>
    public PushStreamContent(Func<Stream, Task> onStreamAvailable)
    {
        _onStreamAvailable = onStreamAvailable;
    }

    /// <summary>
    /// When this method is called, it calls the action provided in the constructor with the output
    /// stream to write to. The action must not close or dispose the stream. Once the task completes,
    /// it will close this content instance and complete the HTTP request or response.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to which to write.</param>
    /// <param name="context">The associated <see cref="TransportContext"/>.</param>
    /// <returns>A <see cref="Task"/> instance that is asynchronously serializing the object's content.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is passed as task result.")]
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        // Note the callee must not close or dispose the stream because they don't own it
        // We _could_ use a wrapper stream to enforce that, but then it _requires_ the
        // callee to call close/dispose which seems weird
        return _onStreamAvailable(stream);
    }

    /// <summary>
    /// Computes the length of the stream if possible.
    /// </summary>
    /// <param name="length">The computed length of the stream.</param>
    /// <returns><c>true</c> if the length has been computed; otherwise <c>false</c>.</returns>
    protected override bool TryComputeLength(out long length)
    {
        // We can't know the length of the content being pushed to the output stream.
        length = -1;
        return false;
    }
}
#endif
