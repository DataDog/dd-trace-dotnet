// <copyright file="DelegatingStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Util.Streams;

/// <summary>
/// A simple <see cref="Stream"/> implementation that delegates to an inner stream implementation
/// </summary>
internal abstract class DelegatingStream(Stream innerStream) : Stream
{
    private readonly Stream _innerStream = innerStream;

    public override bool CanRead => _innerStream.CanRead;

    public override bool CanSeek => _innerStream.CanSeek;

    public override bool CanWrite => _innerStream.CanWrite;

    public override bool CanTimeout => _innerStream.CanTimeout;

    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override int ReadTimeout
    {
        get => _innerStream.ReadTimeout;
        set => _innerStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _innerStream.WriteTimeout;
        set => _innerStream.WriteTimeout = value;
    }

    public override void Flush() => _innerStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

    public override void SetLength(long value) => _innerStream.SetLength(value);

    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);

    public override int ReadByte() => _innerStream.ReadByte();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => _innerStream.BeginRead(buffer, offset, count, callback!, state);

    public override int EndRead(IAsyncResult asyncResult) => _innerStream.EndRead(asyncResult);

    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

    public override void WriteByte(byte value) => _innerStream.WriteByte(value);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => _innerStream.BeginWrite(buffer, offset, count, callback!, state);

    public override void EndWrite(IAsyncResult asyncResult) => _innerStream.EndWrite(asyncResult);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

#if NETCOREAPP
    public override ValueTask DisposeAsync() => _innerStream.DisposeAsync();
#endif
}
