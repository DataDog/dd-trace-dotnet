// <copyright file="AppSecWafBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent2]
public class AppSecWafBenchmark
{
    public const int TimeoutMicroSeconds = 1_000_000;

    private readonly Waf _waf;
    private Context _context;

    public AppSecWafBenchmark()
    {
        var libInitResult = WafLibraryInvoker.Initialize();
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        var wafLibraryInvoker = libInitResult.WafLibraryInvoker!;
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty);
        _waf = initResult.Waf;
    }

    public IEnumerable<Dictionary<string, object>> Source() // for multiple arguments it's an IEnumerable of array of objects (object[])
    {
        var dic1 = MakeNestedMap(10);
        var dic2 = MakeNestedMap(100);
        var dic3 = MakeNestedMap(1000);
        yield return dic1;
        yield return dic2;
        yield return dic3;
    }

    [GlobalSetup]
    public void Setup()
    {
        _context = _waf.CreateContext() as Context;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [Benchmark]
    [ArgumentsSource(nameof(Source))]
    public void RunWafWithBindingsFromWaf(Dictionary<string, object> args)
    {
        _context.Run(args, TimeoutMicroSeconds);
    }

    private static Dictionary<string, object> MakeNestedMap(int nestingDepth)
    {
        var root = new Dictionary<string, object>();
        var map = root;

        for (var i = 0; i < nestingDepth; i++)
        {
            if (i % 2 == 0)
            {
                var nextList = new List<object>
                {
                    true,
                    false,
                    true,
                    123,
                    "lorem",
                    "ipsum",
                    "dolor",
                    AddressesConstants.RequestCookies, new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } }
                };
                map.Add("list", nextList);
            }

            var nextMap = new Dictionary<string, object>
            {
                { "lorem", "ipsum" },
                { "dolor", "sit" },
                { "amet", "amet" },
                { "lorem2", "dolor2" },
                { "sit2", true },
                { "amet3", 4356 }
            };
            map.Add("item", nextMap);
            map = nextMap;
        }

        return root;
    }
}
