// <copyright file="AppSecWafBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent7]
[MaxIterationCount(30)]
[MaxWarmupCount(10)]
public class AppSecWafBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    private static readonly Waf Waf;
    private Context _context;

    static AppSecWafBenchmark()
    {
        var fDesc = FrameworkDescription.Instance;
        var rid = (fDesc.ProcessArchitecture, fDesc.OSPlatform) switch
        {
            ("x64", "Windows") => "win-x64",
            ("x86", "Windows") => "win-x86",
            ("x64", "Linux") => "linux-x64",
            ("arm64", "Linux") => "linux-arm64",
            _ => throw new Exception($"RID not detected or supported: {fDesc.OSPlatform} / {fDesc.ProcessArchitecture}")
        };

        var folder = new DirectoryInfo(Environment.CurrentDirectory);
        var path = Environment.CurrentDirectory;
        while (folder.Exists)
        {
            path = Path.Combine(folder.FullName, "./shared/bin/monitoring-home");
            if (Directory.Exists(path))
            {
                break;
            }

            if (folder == folder.Parent)
            {
                break;
            }

            folder = folder.Parent;
        }
        
        path = Path.Combine(path, $"./{rid}/");
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The Path: '{path}' doesn't exist.");
        }

        Environment.SetEnvironmentVariable("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH", path);
        var libInitResult = WafLibraryInvoker.Initialize();
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        var wafLibraryInvoker = libInitResult.WafLibraryInvoker!;
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty);
        Waf = initResult.Waf;
    }

    public IEnumerable<Dictionary<string, object>> Source()
    {
        yield return MakeNestedMap(400);
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
                    false,
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

    [GlobalSetup]
    public void Setup()
    {
        _context = Waf.CreateContext() as Context;
    }

    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    [Benchmark]
    [ArgumentsSource(nameof(Source))]
    public void RunWaf(Dictionary<string, object> args) => _context.Run(args, TimeoutMicroSeconds);
}
