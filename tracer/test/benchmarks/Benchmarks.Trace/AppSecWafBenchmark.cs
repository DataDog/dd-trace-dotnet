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
[BenchmarkAgent2]
[MaxIterationCount(30)]
[MaxWarmupCount(10)]
public class AppSecWafBenchmark
{
    public const int TimeoutMicroSeconds = 1_000_000;

    private readonly Waf _waf;
    private Context _context;

    public AppSecWafBenchmark()
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
        _waf = initResult.Waf;
    }

    public IEnumerable<Dictionary<string, object>> Source()
    {
       yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.RequestMethod, "GET" },
        };
        yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "x_filename", "routing.yml" } } },
            {
                AddressesConstants.RequestBody, new Dictionary<string, object>
                {
                    { "value1", "adsensepostnottherenonobook" },
                    { "value5", true },
                    { "value6", false },
                    { "value2", "security_scanner2" },
                    { "value3", "security_scanner3" },
                    { "value4", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } } } }
                }
            },
            { AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "something", "appscan_fingerprint" }, { "something2", true }, { "something3", true }, { "something4", true }, { "something5", true }, { "so", new List<string> { "test", "test2", "test3", "test4" } } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/" },
            { AddressesConstants.RequestMethod, "GET" },
        };
        yield return new Dictionary<string, object>
        {
            { AddressesConstants.RequestHeaderNoCookies, new Dictionary<string, string> { { "x_filename", "routing.yml" }, { "x_filename2", "hello there very long string we have here now and it's potentially an attack" } } },
            { AddressesConstants.RequestBodyFileFieldNames, new Dictionary<string, string> { { "x_filename2", "routing.yml2" } } },
            {
                AddressesConstants.RequestBody, new Dictionary<string, object>
                {
                    { "value1", "adsensepostnottherenonobook" },
                    { "value5", true },
                    { "value6", false },
                    { "value2", "security_scanner2" },
                    { "value3", "security_scanner3" },
                    { "key", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } }, { "test4", new List<object> { true, true, true, false, true, 1234} } } },
                    { "value4", new Dictionary<string, object> { { "test1", "test2" }, { "test3", new List<string> { "test", "test2", "test3" } }, { "test4", new List<object> { true, true, true, false, true, 1234} } } }
                }
            },
            { AddressesConstants.RequestPathParams, new Dictionary<string, object> { { "something", "appscan_fingerprint" }, { "something2", true }, { "something3", true }, { "something4", true }, { "something5", true }, { "so", new List<string> { "test", "test2", "test3", "test4" } } } },
            { AddressesConstants.RequestCookies, new Dictionary<string, object> { { "something", ".htaccess" }, { "something2", ";shutdown--" }, {"test", new List<bool> { true, false, true, true, true } } } },
            { AddressesConstants.RequestQuery, new Dictionary<string, string> { { "[$ne]", "appscan_fingerprint" }, { "arg!", "appscan_fingerprint" } } },
            { AddressesConstants.RequestUriRaw, "http://localhost:54587/lalallala" },
            { AddressesConstants.RequestMethod, "POST" },
        };
    }

    public IEnumerable<Dictionary<string, object>> Source2()
    {
        yield return MakeNestedMap(10);
        yield return MakeNestedMap(100);
        yield return MakeNestedMap(1000);
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
        _context = _waf.CreateContext() as Context;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [Benchmark]
    [ArgumentsSource(nameof(Source2))]
    public void RunWaf(Dictionary<string, object> args)
    {
        _context.Run(args, TimeoutMicroSeconds);
    }
}
