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
    /// ULEB128 delta-varint + base64 codec for FFE APM span enrichment.
    /// Ported verbatim from the frozen Node reference (dd-trace-js#8343): dedupe → sort
    /// ascending → delta-from-previous → unsigned LEB128 (7 bits/byte, MSB = continuation)
    /// → base64. The empty set encodes to the empty string (the tag is then omitted).
    /// Allocation-conscious: no LINQ, varint bytes are written straight into the payload buffer.
    /// Runs on the root-span-finish hot path.
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

            // Dedupe + sort ascending (structural, matching the Node Set semantics): copy into an
            // array and sort in place, then skip adjacent duplicates while encoding. This avoids
            // allocating a SortedSet just to order the ids.
            var ids = new long[serialIds.Count];
            var index = 0;
            foreach (var id in serialIds)
            {
                ids[index++] = id;
            }

            Array.Sort(ids);

            // Worst case is MaxVarintBytes per id; this single buffer holds the whole payload and
            // is written into directly (no per-id scratch buffer).
            var buffer = new byte[ids.Length * MaxVarintBytes];
            var written = 0;
            long prev = 0;

            for (var i = 0; i < ids.Length; i++)
            {
                if (i > 0 && ids[i] == ids[i - 1])
                {
                    continue; // dedupe: adjacent equal ids collapse to nothing
                }

                var delta = ids[i] - prev;
                prev = ids[i];
                written += EncodeVarint((ulong)delta, buffer, written);
            }

            return Convert.ToBase64String(buffer, 0, written);
        }

        // ULEB128: emit 7 low bits per byte, set the MSB while more bits remain. Writes into
        // destination starting at offset and returns the number of bytes written.
        private static int EncodeVarint(ulong value, byte[] destination, int offset)
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
