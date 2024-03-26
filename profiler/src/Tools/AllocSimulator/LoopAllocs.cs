// <copyright file="LoopAllocs.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace AllocSimulator
{
    public class LoopAllocs
    {
        public LoopAllocs()
        {
            Allocations = new List<AllocInfo>();
        }

        public bool IsRandom { get; set; }

        public int Iterations { get; set; }

        public List<AllocInfo> Allocations { get; set; }
    }
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
