// <copyright file="UnmanagedMemoryPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.AppSec.Waf;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class UnmanagedMemoryPoolTests
{
    [Fact]
    public void Test()
    {
        Encoder.SetPoolSize(21);

        var addresses = new Dictionary<string, object>();

        for (int i = 0; i < 10; i++)
        {
            addresses[$"arg{i}"] = $"val{i}";
        }

        var argCache = new List<IntPtr>();
        var pool = Encoder.Pool;
        for (int i = 0; i < 100; i++)
        {
            try
            {
                var pwArgs = Encoder.Encode(addresses, applySafetyLimits: true, argToFree: argCache, pool: pool);
            }
            finally
            {
                pool.Return(argCache);
                argCache.Clear();
            }
        }
    }

    [Fact]
    public void TestMultiThreaded()
    {
        Encoder.SetPoolSize(10);

        var threads = new Thread[20];
        for (int t = 0; t < threads.Length; t++)
        {
            var thread = new Thread(
                () =>
                {
                    var addresses = new Dictionary<string, object>();

                    for (int i = 0; i < 12; i++)
                    {
                        addresses[$"arg{i}"] = $"val{i}";
                    }

                    var argCache = new List<IntPtr>();
                    var pool = Encoder.Pool;
                    for (int i = 0; i < 100_000; i++)
                    {
                        try
                        {
                            var pwArgs = Encoder.Encode(addresses, applySafetyLimits: true, argToFree: argCache, pool: pool);
                        }
                        finally
                        {
                            pool.Return(argCache);
                            argCache.Clear();
                        }
                    }
                });
            threads[t] = thread;
        }

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }
    }
}
