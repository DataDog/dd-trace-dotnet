// <copyright file="UnmanagedMemoryPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#if NETFRAMEWORK
using System.Web.Routing;
#else
using Microsoft.AspNetCore.Routing;
#endif
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Util;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class UnmanagedMemoryPoolTests
{
    private readonly Encoder _encoder;

    public UnmanagedMemoryPoolTests()
    {
        _encoder = new Encoder();
    }

    [Fact]
    public void TestRentReturn()
    {
        var unmanagedPool = new UnmanagedMemoryPool(100, 10);
        var arg = new List<IntPtr>();
        for (var i = 0; i < 40; i++)
        {
            arg.Add(unmanagedPool.Rent());
        }

        for (var i = 0; i < 20; i++)
        {
            var ptr = arg[i];
            unmanagedPool.Return(ptr);
        }

        for (var i = 20; i < 40; i++)
        {
            var ptr = arg[i];
            unmanagedPool.Return(ptr);
        }

        unmanagedPool.Dispose();
    }

    [Fact]
    public void TestRentReturnList()
    {
        var unmanagedPool = new UnmanagedMemoryPool(100, 10);
        var arg = new List<IntPtr>();
        for (var i = 0; i < 30; i++)
        {
            arg.Add(unmanagedPool.Rent());
        }

        unmanagedPool.Return(arg);
        unmanagedPool.Dispose();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    public void TestWithEncoder(int? poolSize)
    {
        var addresses = new Dictionary<string, object>();

        var dic = new Dictionary<string, object>();
        for (var j = 0; j < 20; j++)
        {
            dic[$"key-simple{j}"] = 5.2;
            dic[$"key{j}"] = new Dictionary<string, object> { { "dog", new ArrayList { "\r\n7w", "\r\n7w1", "\r\n7w3" } } };
        }

        addresses["body"] = dic;

        var argCache = new List<IntPtr>();
        if (poolSize is not null)
        {
            Encoder.SetPoolSize(poolSize.Value);
        }

        var pool = Encoder.Pool;

        try
        {
            var pwArgs = _encoder.Encode(addresses, applySafetyLimits: true, argToFree: argCache, pool: pool);
        }
        finally
        {
            pool.Return(argCache);
            argCache.Clear();
        }

        pool.Dispose();
    }

    [Fact]
    public void TestMultiThreaded()
    {
        var threads = new Thread[50];
        for (var t = 0; t < threads.Length; t++)
        {
            var thread = new Thread(
                () =>
                {
                    var addresses = new Dictionary<string, object>();

                    for (int i = 0; i < 12; i++)
                    {
                        addresses[$"arg{i}list"] = new List<int>() { 1, 2, 3, 4 };
                        addresses[$"arg{i}list2"] = new List<string>() { "test", "test2" };
                        addresses[$"arg{i}dic"] = new Dictionary<string, object>()
                        {
                            { "test", "test2" },
                            { "test2", new Dictionary<string, object> { { "test", 2 } } },
                            {
                                "test3", new Dictionary<string, object>
                                {
                                    { "test", 2 },
                                    { "test2", 2 },
                                    { "test3", 2 },
                                    {
                                        "test4", new List<string>
                                        {
                                            "test",
                                            "test2",
                                            "test",
                                            "test2",
                                            "test",
                                            "test2",
                                            "test",
                                            "test2",
                                            "test",
                                            "test2",
                                            "test",
                                            "test2"
                                        }
                                    }
                                }
                            }
                        };
                    }

                    var argCache = new List<IntPtr>();
                    var pool = Encoder.Pool;
                    for (var i = 0; i < 100; i++)
                    {
                        try
                        {
                            var pwArgs = _encoder.Encode(addresses, applySafetyLimits: false, argToFree: argCache, pool: pool);
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
