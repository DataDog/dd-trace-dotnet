// <copyright file="AllocateArraysOfDoubles.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Allocate
{
    public class AllocateArraysOfDoubles : IAllocations
    {
        public void Allocate(int count)
        {
            List<double[]> arrays = new List<double[]>(count);

            for (int i = 0; i < count; i++)
            {
                arrays.Add(new double[1] { i });
            }

            Console.WriteLine($"Sum {arrays.Count} arrays of one double = {arrays.Sum(doubles => doubles[0])}");
        }
    }
}
