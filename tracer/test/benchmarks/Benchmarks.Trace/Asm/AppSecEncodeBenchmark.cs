// <copyright file="AppSecEncoderBenchmark.cs" company="Datadog">
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
using Datadog.Trace.AppSec.WafEncoding;

namespace Benchmarks.Trace.Asm;

[MemoryDiagnoser]
[BenchmarkAgent7]
[BenchmarkCategory(Constants.AppSecCategory)]
public class AppSecEncoderBenchmark
{
    private static readonly Encoder _encoder;
    private static readonly EncoderLegacy _encoderLegacy;
    private static readonly NestedMap _args;

    static AppSecEncoderBenchmark()
    {
        AppSecBenchmarkUtils.SetupDummyAgent();
        _encoder = new Encoder();
        var wafLibraryInvoker = AppSecBenchmarkUtils.CreateWafLibraryInvoker();
        _encoderLegacy = new EncoderLegacy(wafLibraryInvoker);
        _args = MakeNestedMap(20);
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
                    "sit",
                    "amet",
                    "lorem",
                    "ipsum",
                    "dolor",
                    "sit",
                    "amet",
                    AddressesConstants.RequestCookies,
                    new Dictionary<string, string> { { "something", ".htaccess" }, { "something2", ";shutdown--" } }
                };
                map.Add("list", nextList);
            }

            var nextMap = new Dictionary<string, object>
            {
                { "lorem", "ipsum" },
                { "dolor", "sit" },
                { "ipsum", "sit" },
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
    public void EncodeArgs()
    {
        using var _ = _encoder.Encode(_args.Map, applySafetyLimits: true);
    }

    [Benchmark]
    public void EncodeLegacyArgs()
    {
        using var _ = _encoderLegacy.Encode(_args.Map, applySafetyLimits: true);
    }

    public record NestedMap(Dictionary<string, object> Map, int NestingDepth, bool IsAttack = false)
    {
        public override string ToString() => IsAttack ? $"NestedMap ({NestingDepth}, attack)" : $"NestedMap ({NestingDepth})";
    }
}
