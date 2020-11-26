using System.Collections.Generic;

namespace Datadog.Trace.RuntimeMetrics
{
    internal readonly struct HeapStats
    {
        public readonly ulong Gen0Size;
        public readonly ulong Gen1Size;
        public readonly ulong Gen2Size;
        public readonly ulong LohSize;

        public HeapStats(ulong gen0Size, ulong gen1Size, ulong gen2Size, ulong lohSize)
        {
            Gen0Size = gen0Size;
            Gen1Size = gen1Size;
            Gen2Size = gen2Size;
            LohSize = lohSize;
        }

        public static HeapStats FromPayload(IReadOnlyList<object> payload)
        {
            var gen0Size = (ulong)payload[0];
            var gen1Size = (ulong)payload[2];
            var gen2Size = (ulong)payload[4];
            var lohSize = (ulong)payload[6];

            return new HeapStats(gen0Size, gen1Size, gen2Size, lohSize);
        }
    }
}
