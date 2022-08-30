// <copyright file="ThreadSafeRandom.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;

namespace Datadog.Trace.Util;

internal static class ThreadSafeRandom
{
#if NET6_0_OR_GREATER
    public static int Next(int maxValue) => Random.Shared.Next(maxValue);

    public static int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);

    public static double NextDouble() => Random.Shared.NextDouble();
#else
    private static readonly Random Global = new();

    [ThreadStatic]
    private static Random? _local;

    private static Random Local
    {
        get
        {
            if (_local is null)
            {
                int seed;
                lock (Global)
                {
                    seed = Global.Next();
                }

                _local = new Random(seed);
            }

            return _local;
        }
    }

    public static int Next(int maxValue)
    {
        return Local.Next(maxValue);
    }

    public static int Next(int minValue, int maxValue)
    {
        return Local.Next(minValue, maxValue);
    }

    public static double NextDouble()
    {
        return Local.NextDouble();
    }
#endif
}
