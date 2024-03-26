// <copyright file="MeasureAllocations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS0169 // Remove unused private members
#pragma warning disable IDE0049 // Simplify Names

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    // IDEA: how to measure the impact of AllocationTick sampling to allocations count
    //  - have classes with different sizes (2, 4, 8, 16, 32, 64, 128)
    //  - allocate same count of objects randomly
    //  - allocate different count of objects in different orders
    public class MeasureAllocations : ScenarioBase
    {
        public override void OnProcess()
        {
            Allocate();
        }

        private void Allocate()
        {
            Dictionary<string, AllocStats> allocations = Initialize();
            List<Object0> objects = new List<Object0>(1024 * 1024);

            AllocateRandom(100_000, objects, allocations);
            Console.WriteLine();
            AllocateSmallThenBig(100_000, objects, allocations);
            Console.WriteLine();
            AllocateBigThenSmall(100_000, objects, allocations);
            Console.WriteLine();
        }

        private void AllocateRandom(int totalAllocations, List<Object0> objects, Dictionary<string, AllocStats> allocations)
        {
            // allocate randomly among 2, 4, 8, 16, 32, 64, 128 sized classes
            Random r = new Random(DateTime.Now.Millisecond);
            for (int i = 0; i < totalAllocations; i++)
            {
                int type = r.Next(7);
                var (instance, size) = Allocate(type);
                objects.Add(instance);
                var typeName = instance.GetType().Name;
                var count = allocations[typeName].Count + 1;
                allocations[typeName].Count = count;
                size += allocations[typeName].Size;
                allocations[typeName].Size = size;
            }

            DumpAllocations(allocations);
            Clear(allocations);
            objects.Clear();
        }

        private void AllocateSmallThenBig(int iterations, List<Object0> objects, Dictionary<string, AllocStats> allocations)
        {
            for (int i = 0; i < iterations; i++)
            {
                // allocate from smaller to larger
                Object0 instance = new Object2();
                objects.Add(instance);
                instance = new Object4();
                objects.Add(instance);
                instance = new Object8();
                objects.Add(instance);
                instance = new Object16();
                objects.Add(instance);
                instance = new Object32();
                objects.Add(instance);
                instance = new Object64();
                objects.Add(instance);
                instance = new Object128();
                objects.Add(instance);
            }

            allocations[nameof(Object2)].Count = iterations;
            allocations[nameof(Object2)].Size = iterations * 2;
            allocations[nameof(Object4)].Count = iterations;
            allocations[nameof(Object4)].Size = iterations * 4;
            allocations[nameof(Object8)].Count = iterations;
            allocations[nameof(Object8)].Size = iterations * 8;
            allocations[nameof(Object16)].Count = iterations;
            allocations[nameof(Object16)].Size = iterations * 16;
            allocations[nameof(Object32)].Count = iterations;
            allocations[nameof(Object32)].Size = iterations * 32;
            allocations[nameof(Object64)].Count = iterations;
            allocations[nameof(Object64)].Size = iterations * 64;
            allocations[nameof(Object128)].Count = iterations;
            allocations[nameof(Object128)].Size = iterations * 128;

            DumpAllocations(allocations);
            Clear(allocations);
            objects.Clear();
        }

        private void AllocateBigThenSmall(int iterations, List<Object0> objects, Dictionary<string, AllocStats> allocations)
        {
            for (int i = 0; i < iterations; i++)
            {
                // allocate from larger to smaller
                Object0 instance = new Object128();
                objects.Add(instance);
                instance = new Object64();
                objects.Add(instance);
                instance = new Object32();
                objects.Add(instance);
                instance = new Object16();
                objects.Add(instance);
                instance = new Object8();
                objects.Add(instance);
                instance = new Object4();
                objects.Add(instance);
                instance = new Object2();
                objects.Add(instance);
            }

            allocations[nameof(Object2)].Count = iterations;
            allocations[nameof(Object2)].Size = iterations * 2;
            allocations[nameof(Object4)].Count = iterations;
            allocations[nameof(Object4)].Size = iterations * 4;
            allocations[nameof(Object8)].Count = iterations;
            allocations[nameof(Object8)].Size = iterations * 8;
            allocations[nameof(Object16)].Count = iterations;
            allocations[nameof(Object16)].Size = iterations * 16;
            allocations[nameof(Object32)].Count = iterations;
            allocations[nameof(Object32)].Size = iterations * 32;
            allocations[nameof(Object64)].Count = iterations;
            allocations[nameof(Object64)].Size = iterations * 64;
            allocations[nameof(Object128)].Count = iterations;
            allocations[nameof(Object128)].Size = iterations * 128;

            DumpAllocations(allocations);
            Clear(allocations);
            objects.Clear();
        }

        private Dictionary<string, AllocStats> Initialize()
        {
            var allocations = new Dictionary<string, AllocStats>(16);
            allocations[nameof(Object2)] = new AllocStats();
            allocations[nameof(Object4)] = new AllocStats();
            allocations[nameof(Object8)] = new AllocStats();
            allocations[nameof(Object16)] = new AllocStats();
            allocations[nameof(Object32)] = new AllocStats();
            allocations[nameof(Object64)] = new AllocStats();
            allocations[nameof(Object128)] = new AllocStats();

            Clear(allocations);
            return allocations;
        }

        private void Clear(Dictionary<string, AllocStats> allocations)
        {
            allocations[nameof(Object2)].Count = 0;
            allocations[nameof(Object2)].Size = 0;
            allocations[nameof(Object4)].Count = 0;
            allocations[nameof(Object4)].Size = 0;
            allocations[nameof(Object8)].Count = 0;
            allocations[nameof(Object8)].Size = 0;
            allocations[nameof(Object16)].Count = 0;
            allocations[nameof(Object16)].Size = 0;
            allocations[nameof(Object32)].Count = 0;
            allocations[nameof(Object32)].Size = 0;
            allocations[nameof(Object64)].Count = 0;
            allocations[nameof(Object64)].Size = 0;
            allocations[nameof(Object128)].Count = 0;
            allocations[nameof(Object128)].Size = 0;
        }

        private (Object0 Instance, long Size) Allocate(int type)
        {
            if (type == 0)
            {
                return (new Object2(), 2);
            }
            else
            if (type == 1)
            {
                return (new Object4(), 4);
            }
            else
            if (type == 2)
            {
                return (new Object8(), 8);
            }
            else
            if (type == 3)
            {
                return (new Object16(), 16);
            }
            else
            if (type == 4)
            {
                return (new Object32(), 32);
            }
            else
            if (type == 5)
            {
                return (new Object64(), 64);
            }
            else
            if (type == 6)
            {
                return (new Object128(), 128);
            }
            else
            {
                throw new ArgumentOutOfRangeException("type", type, "Type cannot be greater than 128");
            }
        }

        private void DumpAllocations(Dictionary<string, AllocStats> objects)
        {
            Console.WriteLine("Allocations start");
            foreach (var allocation in objects)
            {
                Console.WriteLine($"{allocation.Key}={allocation.Value.Count},{allocation.Value.Size}");
            }

            Console.WriteLine("Allocations end");
        }

        internal class AllocStats
        {
            public int Count { get; set; }
            public long Size { get; set; }
        }

        internal class Object0
        {
        }

        internal class Object2 : Object0
        {
            private readonly byte _x1;
            private readonly byte _x2;
        }

        internal class Object4 : Object0
        {
            private readonly UInt16 _x1;
            private readonly UInt16 _x2;
        }

        internal class Object8 : Object0
        {
            private readonly UInt32 _x1;
            private readonly UInt32 _x2;
        }

        internal class Object16 : Object0
        {
            private readonly UInt64 _x1;
            private readonly UInt64 _x2;
        }

        internal class Object32 : Object0
        {
            private readonly UInt64 _x1;
            private readonly UInt64 _x2;
            private readonly UInt64 _x3;
            private readonly UInt64 _x4;
        }

        internal class Object64 : Object0
        {
            private readonly UInt64 _x1;
            private readonly UInt64 _x2;
            private readonly UInt64 _x3;
            private readonly UInt64 _x4;
            private readonly UInt64 _x5;
            private readonly UInt64 _x6;
            private readonly UInt64 _x7;
            private readonly UInt64 _x8;
        }

        internal class Object128 : Object0
        {
            private readonly UInt64 _x1;
            private readonly UInt64 _x2;
            private readonly UInt64 _x3;
            private readonly UInt64 _x4;
            private readonly UInt64 _x5;
            private readonly UInt64 _x6;
            private readonly UInt64 _x7;
            private readonly UInt64 _x8;
            private readonly UInt64 _x9;
            private readonly UInt64 _x10;
            private readonly UInt64 _x11;
            private readonly UInt64 _x12;
            private readonly UInt64 _x13;
            private readonly UInt64 _x14;
            private readonly UInt64 _x15;
            private readonly UInt64 _x16;
        }
    }
}
#pragma warning restore IDE0049 // Simplify Names
#pragma warning restore CS0169 // Remove unused private members
