// <copyright file="HeapHistory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
