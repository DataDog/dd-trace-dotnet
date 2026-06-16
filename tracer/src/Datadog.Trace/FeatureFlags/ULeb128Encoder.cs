// <copyright file="ULeb128Encoder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// ULEB128 delta-varint + base64 codec for FFE APM span enrichment (NET-01).
    /// Ported verbatim from the frozen Node reference (dd-trace-js#8343): dedupe → sort
    /// ascending → delta-from-previous → unsigned LEB128 (7 bits/byte, MSB = continuation)
    /// → base64. The empty set encodes to the empty string (the tag is then omitted).
    /// Allocation-conscious: no LINQ in the encode loop; a stackalloc scratch buffer is
    /// used for the per-id varint bytes. Runs on the root-span-finish hot path.
    /// </summary>
    internal static class ULeb128Encoder
    {
        // A 64-bit value needs at most ceil(64/7) = 10 ULEB128 bytes.
        private const int MaxVarintBytes = 10;

        /// <summary>
        /// Encodes a collection of serial ids (possibly unsorted, with duplicates) into a
        /// bare base64 ULEB128 delta-varint string. Dedupe + sort are performed here so the
        /// encoder owns the structural dedupe contract.
        /// </summary>
        /// <param name="serialIds">The serial ids to encode.</param>
        /// <returns>The base64-encoded string, or <see cref="string.Empty"/> when there are no ids.</returns>
        public static string EncodeDeltaVarint(IReadOnlyCollection<long> serialIds)
        {
            if (serialIds is null || serialIds.Count == 0)
            {
                return string.Empty;
            }

            // Dedupe + sort ascending (structural, matching the Node Set semantics).
            var sorted = new SortedSet<long>(serialIds);
            return EncodeDeltaVarint(sorted);
        }

        /// <summary>
        /// Encodes an already-sorted, already-deduped set of serial ids into a bare base64
        /// ULEB128 delta-varint string. Used by the accumulator, which maintains a
        /// <see cref="SortedSet{T}"/> directly to avoid re-sorting on the hot path.
        /// </summary>
        /// <param name="sortedSerialIds">The sorted, deduped serial ids.</param>
        /// <returns>The base64-encoded string, or <see cref="string.Empty"/> when the set is empty.</returns>
        public static string EncodeDeltaVarint(SortedSet<long> sortedSerialIds)
        {
            if (sortedSerialIds is null || sortedSerialIds.Count == 0)
            {
                return string.Empty;
            }

            // Worst case is MaxVarintBytes per id; this single buffer holds the whole payload.
            var buffer = new byte[sortedSerialIds.Count * MaxVarintBytes];
            var written = 0;
            long prev = 0;

#if NETCOREAPP
            Span<byte> scratch = stackalloc byte[MaxVarintBytes];
#else
            var scratch = new byte[MaxVarintBytes];
#endif

            foreach (var id in sortedSerialIds)
            {
                var delta = id - prev;
                prev = id;

                var n = EncodeVarint((ulong)delta, scratch);
                for (var i = 0; i < n; i++)
                {
                    buffer[written++] = scratch[i];
                }
            }

            return Convert.ToBase64String(buffer, 0, written);
        }

        // ULEB128: emit 7 low bits per byte, set the MSB while more bits remain.
#if NETCOREAPP
        private static int EncodeVarint(ulong value, Span<byte> destination)
#else
        private static int EncodeVarint(ulong value, byte[] destination)
#endif
        {
            var i = 0;
            while (value > 0x7F)
            {
                destination[i++] = (byte)((value & 0x7F) | 0x80);
                value >>= 7;
            }

            destination[i++] = (byte)(value & 0x7F);
            return i;
        }
    }
}
