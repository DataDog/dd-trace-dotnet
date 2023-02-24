// <copyright file="MemoryLeak.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace Samples.Computer01
{
    public class MemoryLeak : ScenarioBase
    {
        // The array will always trigger an AllocationTick event since 100KB is the threshold
        // and a few elements will also trigger the event
        private const int BufferSize = (100 * 1024) + 1;

        private int _objectsToAllocateCount;

        public MemoryLeak(int parameter)
        {
            _objectsToAllocateCount = parameter;
        }

        public override void OnProcess()
        {
            AllocateWithLeak();
        }

        private void AllocateWithLeak()
        {
            List<byte[]> root = new List<byte[]>();
            int count = 0;

            while (!IsEventSet() && (count <= _objectsToAllocateCount))
            {
                try
                {
                    root.Add(new byte[BufferSize]);
                    GC.Collect();

                    count++;
                }
                catch (OutOfMemoryException)
                {
                    // deal with Out Of Memory exceptions (important for x86 tests)
                    root.Clear();
                    GC.Collect();
                }
            }
        }
    }
}
