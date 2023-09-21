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
        var initResult = Waf.Create(wafLibraryInvoker, string.Empty, string.Empty, embeddedRulesetPath: Path.Combine(Directory.GetCurrentDirectory(), "Asm", "rule-set.1.7.2.json"));

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

    [Benchmark]
    [ArgumentsSource(nameof(Source))]
    public void RunWaf(NestedMap args) => RunWafBenchmark(args);

    [Benchmark]
    [ArgumentsSource(nameof(SourceWithAttack))]
    public void RunWafWithAttack(NestedMap args) => RunWafBenchmark(args);

    private void RunWafBenchmark(NestedMap args)
    {
        var context = Waf.CreateContext();
        context!.Run(args.Map, TimeoutMicroSeconds);
        context.Dispose();
    }

    public record NestedMap(Dictionary<string, object> Map, int NestingDepth, bool IsAttack = false)
    {
        public override string ToString() => IsAttack ? $"NestedMap ({NestingDepth}, attack)" : $"NestedMap ({NestingDepth})";
    }
}
