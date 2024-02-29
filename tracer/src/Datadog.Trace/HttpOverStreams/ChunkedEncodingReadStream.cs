// <copyright file="ChunkedEncodingReadStream.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util.Streams;

namespace Datadog.Trace.HttpOverStreams;

internal sealed partial class ChunkedEncodingReadStream : DelegatingStream
{
    private const int ReadBufferSize =
#if DEBUG
        MaxChunkBytesAllowed * 2;
#else
        4096;
#endif

    /// <summary>How long a chunk indicator is allowed to be.</summary>
    /// <remarks>
    /// While most chunks indicators will contain no more than ulong.MaxValue.ToString("X").Length characters (i.e. 16),
    /// "chunk extensions" are allowed. We place a limit on how long a line can be to avoid OOM issues if an
    /// infinite chunk length is sent.  This value is arbitrary and can be changed as needed.
    /// As we're not trying to handle extensions, we've set this to an arbitrary low limit
    /// NOTE: this number needs to be &lt; ReadBufferSize * 2 to work with the current implementation
    /// </remarks>
    private const int MaxChunkBytesAllowed =
#if DEBUG
        32;
#else
        128;
#endif

    private readonly Stream _innerStream;
    private readonly byte[] _streamBuffer;
    private ulong _bytesRemainingInChunk;

    // The "unconsumed" data in _streamBuffer
    private Segment _currentPosition;

    /// <summary>The current state of the parsing state machine for the chunked response.</summary>
    private ParsingState _state = ParsingState.ExpectChunkHeader;

    public ChunkedEncodingReadStream(Stream innerStream)
        : base(innerStream)
    {
        _innerStream = innerStream;
        // We use a buffer that is double the size of the read buffer, so that we can move
        // the bytes around if we end up hitting edge cases
        _streamBuffer = new byte[ReadBufferSize * 2];
        _currentPosition = new(offset: 0, count: 0);
    }

    private enum ParsingState : byte
    {
        ExpectChunkHeader,
        ExpectChunkData,
        ExpectChunkTerminator,
        // For "simplicity" we don't support trailers currently
        // ConsumeTrailers,
        Done
    }

    // This is not called by our production code, so we're yolo-ing it
    // MockTracerAgent currently _does_ use this code path
    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer, offset, count, default).GetAwaiter().GetResult();

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            Segment currentLine;
            switch (_state)
            {
                case ParsingState.ExpectChunkHeader:
                    Debug.Assert(_bytesRemainingInChunk == 0, $"Expected {nameof(_bytesRemainingInChunk)} == 0, got {_bytesRemainingInChunk}");

                    // Try to read the chunk header from the bytes we have available
                    while (!TryReadNextChunkLine(out currentLine))
                    {
                        // We didn't have enough data to read the chunk header, so we need to refill the buffer
                        // and try again
                        await FillAsync().ConfigureAwait(false);
                    }

                    // We have a chunk header, so we can parse it
                    _bytesRemainingInChunk = ParseChunkHexString(_streamBuffer, currentLine.Offset, currentLine.Length);

                    // Proceed to handle the chunk.  If there's data in it, go read it.
                    // Otherwise, finish handling the response.
                    if (_bytesRemainingInChunk > 0)
                    {
                        _state = ParsingState.ExpectChunkData;
                        break;
                    }

                    // We don't support trailers currently, so we expect another CRLF (chunk terminator)
                    while (!TryReadNextChunkLine(out currentLine))
                    {
                        // We didn't have enough data to read the chunk terminator, so we need to refill the buffer
                        // and try again
                        await FillAsync().ConfigureAwait(false);
                    }

                    if (currentLine.Length != 0)
                    {
                        throw new Exception("Invalid response chunk terminator: expected 0 length terminator, received " + Encoding.ASCII.GetString(_streamBuffer, currentLine.Offset, currentLine.Length));
                    }

                    // all done!
                    _state = ParsingState.Done;
                    return 0;

                case ParsingState.ExpectChunkData:
                    // Read and return the chunk bytes
                    // This reads the data from the stream directly into the provided buffer, instead
                    // of into the _streamBuffer

                    // As an optimization, we skip going through the connection's read buffer if both
                    // the remaining chunk data and the buffer are both at least as large
                    // as the connection buffer.  That avoids an unnecessary copy while still reading
                    // the maximum amount we'd otherwise read at a time.
                    if (_currentPosition.Length == 0
                     && count >= ReadBufferSize
                     && _bytesRemainingInChunk >= ReadBufferSize)
                    {
                        // We don't have enough for the whole chunk, so just read what we can, directly into the output buffer
                        var bytesToRead = (int)Math.Min((ulong)count, _bytesRemainingInChunk);
                        var bytesReadFromStream = await _innerStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken).ConfigureAwait(false);

                        // we might not have read the whole expected values, so just return what we have
                        _bytesRemainingInChunk -= (ulong)bytesReadFromStream;
                        if (_bytesRemainingInChunk == 0)
                        {
                            _state = ParsingState.ExpectChunkTerminator;
                        }

                        return bytesReadFromStream;
                    }

                    if (_currentPosition.Length == 0)
                    {
                        // we have no data, so fill in some more
                        await FillAsync().ConfigureAwait(false);
                    }

                    Debug.Assert(_currentPosition.Length > 0, "We should have some bytes now");

                    // can't fill a full line with chunk data, so instead we need to consume from the existing _buffer
                    var bytesToConsume = Math.Min(count, (int)Math.Min((ulong)_currentPosition.Length, _bytesRemainingInChunk));

                    Debug.Assert(offset + bytesToConsume <= buffer.Length, "Should not try to consume more data than we have space to copy into");
                    Debug.Assert(_currentPosition.Offset + bytesToConsume <= _streamBuffer.Length, "Should not try to consume more data than we have to read from");

                    // Copy the data we have into the buffer
                    Array.Copy(
                        sourceArray: _streamBuffer,
                        sourceIndex: _currentPosition.Offset,
                        destinationArray: buffer,
                        destinationIndex: offset,
                        length: bytesToConsume);

                    // update the currentPosition and expected bytes to reflect the consumed bytes
                    _bytesRemainingInChunk -= (ulong)bytesToConsume;
                    _currentPosition = new(_currentPosition.Offset + bytesToConsume, _currentPosition.Length - bytesToConsume);

                    if (_bytesRemainingInChunk == 0)
                    {
                        _state = ParsingState.ExpectChunkTerminator;
                    }

                    return bytesToConsume;
                case ParsingState.ExpectChunkTerminator:
                    Debug.Assert(_bytesRemainingInChunk == 0, $"Expected {nameof(_bytesRemainingInChunk)} == 0, got {_bytesRemainingInChunk}");

                    while (!TryReadNextChunkLine(out currentLine))
                    {
                        // We didn't have enough data to read the chunk terminator, so we need to refill the buffer
                        // and try again
                        await FillAsync().ConfigureAwait(false);
                    }

                    if (currentLine.Length != 0)
                    {
                        throw new Exception("Invalid response chunk terminator: expected 0 length terminator, received " + Encoding.ASCII.GetString(_streamBuffer, currentLine.Offset, currentLine.Length));
                    }

                    _state = ParsingState.ExpectChunkHeader;
                    break;

                case ParsingState.Done:
                    // This _shouldn't_ be called once we're done, but a badly behaved
                    // parser may call read on the stream again after it's already received
                    // a 0 for the end of the stream
                    return 0;
            }
        }
    }

    private bool TryReadNextChunkLine(out Segment chunkHeader)
    {
        // try to read the data we have in _currentPosition for a `\n`
        // if we don't have anything in the buffer, then we need to do a fetch

        // This will actually potentially search _beyond_ the remaining values, but just handle it later
        var lineFeedIndex = Array.IndexOf(_streamBuffer, (byte)'\n', _currentPosition.Offset);
        if (lineFeedIndex < 0 || lineFeedIndex >= _currentPosition.Offset + _currentPosition.Length)
        {
            // didn't find a line feed in the available bytes
            if (_currentPosition.Length < MaxChunkBytesAllowed)
            {
                // In this case, we only have a few bytes that we've looked through
                // So we need to extend the buffer and try again
                chunkHeader = default;
                return false;
            }

            // Oops, we _should_ have found it, so something is wrong here, so throw chunk size too big
        }
        else
        {
            int bytesConsumedIndex = lineFeedIndex + 1;
            if ((bytesConsumedIndex - _currentPosition.Offset) <= MaxChunkBytesAllowed)
            {
                int carriageReturnIndex = lineFeedIndex - 1;

                // Note that this has a bounds check on both the high and lower end
                // if lineFeedIndex == 0, and carriageReturnIndex == -1, then
                // (uint)carriageReturnIndex == uint.MaxValue, and the check will be false
                int lengthFromStart = (uint)carriageReturnIndex < (uint)(_currentPosition.Offset + _currentPosition.Length) && _streamBuffer[carriageReturnIndex] == '\r'
                                 ? carriageReturnIndex
                                 : lineFeedIndex;

                var lineLength = lengthFromStart - _currentPosition.Offset;
                chunkHeader = new Segment(_currentPosition.Offset, lineLength);

                // increment the current position to _after_ the chunk Header
                var shiftBy = bytesConsumedIndex - _currentPosition.Offset;

                _currentPosition = new(bytesConsumedIndex, _currentPosition.Length - shiftBy);
                return true;
            }
        }

        throw new Exception($"Chunk size header was too large: more than maximum of {MaxChunkBytesAllowed} bytes");
    }

    /// <summary>
    /// Trys to fill up the associated buffer segment from the underlying stream
    /// If we still have some existing bytes left over in <see cref="_currentPosition"/>,
    /// these are moved to the start of the array, and preserved
    /// </summary>
    private async Task FillAsync()
    {
        var prependedBytes = 0;
        if (_currentPosition.Length > 0)
        {
            // we still have some bytes remaining, move them to the start
            Array.Copy(
                sourceArray: _streamBuffer,
                sourceIndex: _currentPosition.Offset,
                destinationArray: _streamBuffer,
                destinationIndex: 0,
                length: _currentPosition.Length);

            prependedBytes = _currentPosition.Length;
        }

        var bytesReadFromStream = await _innerStream.ReadAsync(_streamBuffer, offset: prependedBytes, ReadBufferSize).ConfigureAwait(false);

        // this should always return some bytes because we're _expecting_ bytes to be read
        if (bytesReadFromStream == 0)
        {
            throw new EndOfStreamException("Invalid HTTP response - unexpected end of stream");
        }

        // update _currentPosition to include both the prepended bytes and the new bytes
        _currentPosition = new Segment(offset: 0, count: prependedBytes + bytesReadFromStream);
    }

    [DebuggerDisplay("Offset = {Offset}, Length = {Length}")]
    private readonly struct Segment
    {
        public readonly int Offset;
        public readonly int Length;

        public Segment(int offset, int count)
        {
            Offset = offset;
            Length = count;
        }
    }
}
