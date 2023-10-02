// <copyright file="StringInstrumentationBenchmark.cs" company="Datadog">
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

namespace Benchmarks.Trace.Iast;

[MemoryDiagnoser]
[BenchmarkAgent7]
public class StringAspectsBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    static StringAspectsBenchmark()
    {
    }

    public IEnumerable<int> TaintedDictionary()
    {
        yield return MakeTaintedDictionary(10);
        yield return MakeTaintedDictionary(20);
        yield return MakeTaintedDictionary(100);
    }

    /// <summary>
    /// Generates dummy arguments for the waf
    /// </summary>
    /// <param name="nestingDepth">Encoder.cs respects WafConstants.cs limits to process arguments with a max depth of 20 so above depth 20, there shouldn't be much difference of performances.</param>
    /// <param name="withAttack">an attack present in arguments can slow down waf's run</param>
    /// <returns></returns>
    private static int MakeTaintedDictionary(int size)
    {
        return size;
    }

    [Benchmark]
    [ArgumentsSource(nameof(MakeTaintedDictionary))]
    public void RunStringBenchmark(int size)
    { 
    
    }

    [Benchmark]
    [ArgumentsSource(nameof(MakeTaintedDictionary))]
    public void RunStringAspectBenchmark(int size) 
    {
    
    }

    private void RunStringBenchmark(NestedMap args)
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
