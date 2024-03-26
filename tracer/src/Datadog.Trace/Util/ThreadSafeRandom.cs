// <copyright file="ThreadSafeRandom.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Util;

internal static class ThreadSafeRandom
{
#if NET6_0_OR_GREATER
    public static Random Shared => Random.Shared;
#else
    private static readonly Random Global = new();

    [ThreadStatic]
    private static Random? _local;

    public static Random Shared
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

#endif
}
