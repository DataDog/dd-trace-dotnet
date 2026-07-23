// <copyright file="GlobalCoverageBoundedWriteStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;

namespace Datadog.Trace.Ci.Coverage;

// This wrapper intentionally does not own the underlying stream. Callers perform the durable file
// flush after the JSON writers have flushed through this byte-counting layer.
internal sealed class GlobalCoverageBoundedWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private readonly string _limitExceededMessage;
    private long _writtenBytes;

    internal GlobalCoverageBoundedWriteStream(Stream inner, long maximumBytes, string limitExceededMessage)
    {
        _inner = inner;
        _maximumBytes = maximumBytes;
        _limitExceededMessage = limitExceededMessage;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _writtenBytes;

    public override long Position
    {
        get => _writtenBytes;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        var nextLength = checked(_writtenBytes + count);
        if (nextLength > _maximumBytes)
        {
            throw new InvalidDataException(_limitExceededMessage);
        }

        _inner.Write(buffer, offset, count);
        _writtenBytes = nextLength;
    }
}
