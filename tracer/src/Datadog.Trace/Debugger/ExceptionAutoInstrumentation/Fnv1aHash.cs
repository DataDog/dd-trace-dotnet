// <copyright file="Fnv1aHash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class Fnv1aHash
    {
        private const uint FnvPrime = 16777619;
        private const uint OffsetBasis = 2166136261;

        public static uint ComputeHash(uint previousHash, int input)
        {
            var hash = previousHash == 0 ? OffsetBasis : previousHash;

            // Processing the bytes of the integer `input`
            foreach (var data in BitConverter.GetBytes(input))
            {
                hash ^= data;
                hash *= FnvPrime;
            }

            return hash;
        }
    }
}
