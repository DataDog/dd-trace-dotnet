// <copyright file="ULeb128Encoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Buffers;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// ULEB128 delta-varint + base64 codec for FFE APM span enrichment.
    /// Ported verbatim from the frozen Node reference (dd-trace-js#8343): dedupe → sort
    /// ascending → delta-from-previous → unsigned LEB128 (7 bits/byte, MSB = continuation)
    /// → base64. The empty set encodes to the empty string (the tag is then omitted).
    /// Runs on the serializer thread (from <c>SpanMessagePackFormatter</c>).
    /// Allocation-conscious: the sort copy and varint payload use the stack for small sets
    /// (modern runtimes) and a pooled buffer otherwise, so only the base64 result allocates.
    /// </summary>
    internal static class ULeb128Encoder
    {
        // A 64-bit value needs at most ceil(64/7) = 10 ULEB128 bytes.
        private const int MaxVarintBytes = 10;

        // Sets at or below this many ids are encoded entirely on the stack (modern runtimes only):
        // 128 longs (1 KiB) for the sort copy + 128*10 bytes (~1.25 KiB) for the payload. Serial-id
        // sets are bounded by SpanEnrichmentState.MaxSerialIds (200), so the common case stays on
        // the stack; larger sets fall back to the array pool.
        private const int StackAllocMaxIds = 128;

        /// <summary>
        /// Encodes a collection of serial ids (possibly unsorted, with duplicates) into a
        /// bare base64 ULEB128 delta-varint string. Dedupe + sort are performed here so the
        /// encoder owns the structural dedupe contract.
        /// </summary>
        /// <param name="serialIds">The serial ids to encode.</param>
        /// <returns>The base64-encoded string, or <see cref="string.Empty"/> when there are no ids.</returns>
        public static string EncodeDeltaVarint(ReadOnlySpan<long> serialIds)
        {
            if (serialIds.Length == 0)
            {
                return string.Empty;
            }

            var count = serialIds.Length;

#if NETCOREAPP3_1_OR_GREATER
            // Fast path: dedupe + sort + encode entirely on the stack, no pooling, no copy to a
            // heap array. Span.Sort and the ReadOnlySpan base64 overload are both BCL on .NET Core.
            if (count <= StackAllocMaxIds)
            {
                Span<long> ids = stackalloc long[count];
                serialIds.CopyTo(ids);
                ids.Sort();

                Span<byte> payload = stackalloc byte[count * MaxVarintBytes];
                var writtenOnStack = EncodeSorted(ids, payload);
                return Convert.ToBase64String(payload.Slice(0, writtenOnStack));
            }
#endif

            // Fallback (large sets, or .NET Framework / netstandard2.0 which lack Span.Sort and the
            // ReadOnlySpan base64 overload): rent both buffers so nothing beyond the result allocates.
            var idBuffer = ArrayPool<long>.Shared.Rent(count);
            var payloadBuffer = ArrayPool<byte>.Shared.Rent(count * MaxVarintBytes);
            try
            {
                serialIds.CopyTo(idBuffer);
                Array.Sort(idBuffer, 0, count);

                var written = EncodeSorted(new ReadOnlySpan<long>(idBuffer, 0, count), payloadBuffer);
                return Convert.ToBase64String(payloadBuffer, 0, written);
            }
            finally
            {
                ArrayPool<long>.Shared.Return(idBuffer);
                ArrayPool<byte>.Shared.Return(payloadBuffer);
            }
        }

        // Encodes an ascending-sorted span, skipping adjacent duplicates (structural dedupe matching
        // the Node Set semantics), as delta-from-previous ULEB128 varints. Returns the byte count.
        private static int EncodeSorted(ReadOnlySpan<long> sortedIds, Span<byte> destination)
        {
            var written = 0;
            long prev = 0;

            for (var i = 0; i < sortedIds.Length; i++)
            {
                if (i > 0 && sortedIds[i] == sortedIds[i - 1])
                {
                    continue; // dedupe: adjacent equal ids collapse to nothing
                }

                var delta = sortedIds[i] - prev;
                prev = sortedIds[i];
                written += EncodeVarint((ulong)delta, destination, written);
            }

            return written;
        }

        // ULEB128: emit 7 low bits per byte, set the MSB while more bits remain. Writes into
        // destination starting at offset and returns the number of bytes written.
        private static int EncodeVarint(ulong value, Span<byte> destination, int offset)
        {
            var start = offset;
            while (value > 0x7F)
            {
                destination[offset++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }

            destination[offset++] = (byte)(value & 0x7F);
            return offset - start;
        }
    }
}
