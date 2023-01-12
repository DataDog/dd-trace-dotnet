// <copyright file="IdGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Util;

/// <summary>
/// Generates random numbers suitable for use as Datadog trace ids and span ids.
/// </summary>
internal class IdGenerator
{
    /// <summary>
    /// Returns a random number greater than zero and less than Int64.MaxValue.
    /// </summary>
    public static ulong NextUInt64()
    {
        while (true)
        {
#if NET6_0_OR_GREATER
            // random integer in range [0, Int64.MaxValue)
            var value = (ulong)System.Random.Shared.NextInt64();
#else
            // random integer in range [0, UInt64.MaxValue],
            // clear highest bit to make it [0, Int64.MaxValue]
            var value = RandomNumberGenerator.Current.NextUInt64() & 0x7FFFFFFFFFFFFFFF;
#endif

            if (value is > 0 and < long.MaxValue)
            {
                // return value if valid, else try again
                return value;
            }
        }
    }
}
