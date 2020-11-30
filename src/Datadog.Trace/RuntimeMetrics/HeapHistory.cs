using System.Collections.Generic;

namespace Datadog.Trace.RuntimeMetrics
{
    internal readonly struct HeapHistory
    {
        public readonly uint? MemoryLoad;
        public readonly uint Generation;
        public readonly bool Compacting;

        public HeapHistory(uint? memoryLoad, uint generation, bool compacting)
        {
            MemoryLoad = memoryLoad;
            Generation = generation;
            Compacting = compacting;
        }

        public static HeapHistory FromPayload(IReadOnlyList<object> payload)
        {
            var generation = (uint)payload[2];
            var compacting = ((uint)payload[5] & 2) == 2;
            uint? memoryLoad = (uint)payload[8];

            if (memoryLoad == 0)
            {
                memoryLoad = null;
            }

            return new HeapHistory(memoryLoad, generation, compacting);
        }
    }
}
