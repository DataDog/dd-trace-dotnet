// <copyright file="SpanIdGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Util
{
    internal class SpanIdGenerator
    {
        public static ulong CreateNew()
        {
            ulong value;
            do
            {
                long high = ThreadSafeRandom.Next(int.MinValue, int.MaxValue);
                long low = ThreadSafeRandom.Next(int.MinValue, int.MaxValue);

                // Concatenate both values, and truncate the 32 top bits from low
                value = (ulong)(high << 32 | (low & 0xFFFFFFFF)) & 0x7FFFFFFFFFFFFFFF;
            }
            while (value == 0);
            return value;
        }
    }
}
