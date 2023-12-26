// <copyright file="AppSecWafBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Benchmarks.Trace.Asm;

[MemoryDiagnoser]
[BenchmarkAgent7]
public class AppSecWafBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    private static readonly Waf Waf;

    private static readonly NestedMap stage1 = MakeRealisticNestedMapStage1();
    private static readonly NestedMap stage2 = MakeRealisticNestedMapStage2();
    private static readonly NestedMap stage3 = MakeRealisticNestedMapStage3();

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

        Environment.SetEnvironmentVariable("DD_TRACE_LOGGING_RATE", "60");
        Environment.SetEnvironmentVariable("DD_INTERNAL_TRACE_NATIVE_ENGINE_PATH", path);
        var libInitResult = WafLibraryInvoker.Initialize();
        if (!libInitResult.Success)
        {
            throw new ArgumentException("Waf could not load");
        }

        var wafLibraryInvoker = libInitResult.WafLibraryInvoker!;
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty, embeddedRulesetPath: Path.Combine(Directory.GetCurrentDirectory(), "Asm", "rule-set.1.10.0.json"));

        if (!initResult.Success || initResult.HasErrors)
        {
            throw new ArgumentException($"Waf could not initialize, error message is: {initResult.ErrorMessage}");
        }

        Waf = initResult.Waf;
    }

    public IEnumerable<NestedMap> Source()
    {
        yield return MakeNestedMap(10);
        yield return MakeNestedMap(20);
        yield return MakeNestedMap(100);
    }

    public IEnumerable<NestedMap> SourceWithAttack()
    {
        yield return MakeNestedMap(10, true);
        yield return MakeNestedMap(20, true);
        yield return MakeNestedMap(100, true);
    }

    /// <summary>
    /// Generates dummy arguments for the waf
    /// </summary>
    /// <param name="nestingDepth">Encoder.cs respects WafConstants.cs limits to process arguments with a max depth of 20 so above depth 20, there shouldn't be much difference of performances.</param>
    /// <param name="withAttack">an attack present in arguments can slow down waf's run</param>
    /// <returns></returns>
    private static NestedMap MakeNestedMap(int nestingDepth, bool withAttack = false)
    {
        var root = new Dictionary<string, object>();
        var map = root;
        if (withAttack)
        {
            map.Add(
                AddressesConstants.RequestHeaderNoCookies,
                new Dictionary<string, string> { { "user-agent", "Arachni/v1" } }
            );
        }
        else
        {
            map.Add(
                "toto",
                new Dictionary<string, string> { { "user-agent", "tata" } }
            );
        }

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
                    AddressesConstants.RequestCookies,
                    new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } }
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

        return new NestedMap(root, nestingDepth, withAttack);
    }

    private static NestedMap MakeRealisticNestedMapStage1()
    {
        var headersDict = new Dictionary<string, string[]>
        {
            { "header1", new [] { "value1", "value2", "value3", } },
            { "header2", new [] { "value1", "value2", "value3", } },
            { "header3", new [] { "value1", "value2", "value3", } },
        };
        var cookiesDic = new Dictionary<string, List<string>>
        {
            { "cookie1", new List<string> { "value1", "value2", "value3", } },
        };
        var queryStringDic = new Dictionary<string, List<string>>
        {
            { "key1", new List<string> { "value1", "value2", "value3", } },
        };

        var addressesDictionary = new Dictionary<string, object>
        {
            { AddressesConstants.RequestMethod, "GET" },
            { AddressesConstants.ResponseStatus, "200" },
            { AddressesConstants.RequestUriRaw, "/short/url" },
            { AddressesConstants.RequestClientIp, "10.20.30.40" }
        };

        addressesDictionary.Add(AddressesConstants.RequestQuery, queryStringDic);
        addressesDictionary.Add(AddressesConstants.RequestHeaderNoCookies, headersDict);
        addressesDictionary.Add(AddressesConstants.RequestCookies, cookiesDic);

        return new NestedMap(addressesDictionary, 1, false);
    }

    private static NestedMap MakeRealisticNestedMapStage2()
    {
        IDictionary<string, object> pathParams = new Dictionary<string, object>()
        {
            { "id", "22" }
        };

        var args = new Dictionary<string, object>
        {
            { AddressesConstants.RequestPathParams, pathParams }
        };

        return new NestedMap(args, 1, false);
    }

    private static NestedMap MakeRealisticNestedMapStage3()
    {
        var headersDict = new Dictionary<string, string[]>()
        {
            { "header1", new [] { "value1", "value2", "value3", } },
            { "header2", new [] { "value1", "value2", "value3", } },
            { "header3", new [] { "value1", "value2", "value3", } },
        };

        var args = new Dictionary<string, object>
        {
            {
                AddressesConstants.ResponseHeaderNoCookies,
                headersDict
            },
            { AddressesConstants.ResponseStatus, "200" },
        };

        return new NestedMap(args, 1, false);
    }

    [Benchmark]
    public void RunWafRealisticBenchmark()
    {
        var context = Waf.CreateContext();
        context!.Run(stage1.Map, TimeoutMicroSeconds);
        context!.Run(stage2.Map, TimeoutMicroSeconds);
        context!.Run(stage3.Map, TimeoutMicroSeconds);
        context.Dispose();
    }

    public record NestedMap(Dictionary<string, object> Map, int NestingDepth, bool IsAttack = false)
    {
        public override string ToString() => IsAttack ? $"NestedMap ({NestingDepth}, attack)" : $"NestedMap ({NestingDepth})";
    }
}
