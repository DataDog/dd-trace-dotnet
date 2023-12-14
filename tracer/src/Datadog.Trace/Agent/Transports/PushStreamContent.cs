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
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
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
    /// stream to write to. Once the action has completed its work it closes the stream which will
    /// close this content instance and complete the HTTP request or response.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to which to write.</param>
    /// <param name="context">The associated <see cref="TransportContext"/>.</param>
    /// <returns>A <see cref="Task"/> instance that is asynchronously serializing the object's content.</returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Exception is passed as task result.")]
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var serializeToStreamTask = new TaskCompletionSource<bool>();

        var wrappedStream = new CompleteTaskOnCloseStream(stream, serializeToStreamTask);
        await _onStreamAvailable(wrappedStream).ConfigureAwait(false);

        // wait for wrappedStream.Close/Dispose to get called.
        await serializeToStreamTask.Task.ConfigureAwait(false);
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

    private class CompleteTaskOnCloseStream : DelegatingStream
    {
        private readonly TaskCompletionSource<bool> _serializeToStreamTask;

        public CompleteTaskOnCloseStream(Stream innerStream, TaskCompletionSource<bool> serializeToStreamTask)
            : base(innerStream)
        {
            Contract.Assert(serializeToStreamTask != null);
            _serializeToStreamTask = serializeToStreamTask;
        }

        [SuppressMessage(
            "Microsoft.Usage",
            "CA2215:Dispose methods should call base class dispose",
            Justification = "See comments, this is intentional.")]
        protected override void Dispose(bool disposing)
        {
            // We don't dispose the underlying stream because we don't own it. Dispose in this case just signifies
            // that the user's action is finished.
            _serializeToStreamTask.TrySetResult(true);
        }

        public override void Close()
        {
            // We don't Close the underlying stream because we don't own it. Dispose in this case just signifies
            // that the user's action is finished.
            _serializeToStreamTask.TrySetResult(true);
        }
    }

    /// <summary>
    /// Stream that delegates to inner stream.
    /// This is taken from System.Net.Http
    /// </summary>
    private abstract class DelegatingStream : Stream
    {
        private readonly Stream _innerStream;

        protected DelegatingStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        protected Stream InnerStream
        {
            get { return _innerStream; }
        }

        public override bool CanRead
        {
            get { return _innerStream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _innerStream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _innerStream.CanWrite; }
        }

        public override long Length
        {
            get { return _innerStream.Length; }
        }

        public override long Position
        {
            get { return _innerStream.Position; }
            set { _innerStream.Position = value; }
        }

        public override int ReadTimeout
        {
            get { return _innerStream.ReadTimeout; }
            set { _innerStream.ReadTimeout = value; }
        }

        public override bool CanTimeout
        {
            get { return _innerStream.CanTimeout; }
        }

        public override int WriteTimeout
        {
            get { return _innerStream.WriteTimeout; }
            set { _innerStream.WriteTimeout = value; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _innerStream.Seek(offset, origin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if !NETSTANDARD1_3 // BeginX and EndX not supported on Streams in netstandard1.3
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return _innerStream.BeginRead(buffer, offset, count, callback!, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return _innerStream.EndRead(asyncResult);
        }
#endif

        public override int ReadByte()
        {
            return _innerStream.ReadByte();
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _innerStream.FlushAsync(cancellationToken);
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if !NETSTANDARD1_3 // BeginX and EndX not supported on Streams in netstandard1.3
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return _innerStream.BeginWrite(buffer, offset, count, callback!, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            _innerStream.EndWrite(asyncResult);
        }
#endif

        public override void WriteByte(byte value)
        {
            _innerStream.WriteByte(value);
        }
    }
}
#endif
