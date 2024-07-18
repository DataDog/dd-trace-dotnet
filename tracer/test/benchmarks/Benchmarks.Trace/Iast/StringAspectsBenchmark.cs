// <copyright file="StringAspectsBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.Unit.Tests.Iast;


namespace Benchmarks.Trace.Iast;

[MemoryDiagnoser]
[BenchmarkAgent7]
[BenchmarkCategory(Constants.AppSecCategory)]
[IgnoreProfile]
public class StringAspectsBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    private List<string> _initTaintedContextTrue;
    private List<string> _initTaintedContextFalse;
    
    private static List<string> InitTaintedContext(int size, bool initTainted = true)
    {
        TaintedObjects taintedObjects = null;

        if (initTainted)
        {
            var settings = new CustomSettingsForTests(new Dictionary<string, object>()
            {
                { ConfigurationKeys.Iast.RequestSampling, 100 },
                { ConfigurationKeys.Iast.Enabled, true },
                { ConfigurationKeys.Iast.IsIastDeduplicationEnabled, false },
            });
            var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
            Datadog.Trace.Iast.Iast.Instance = new Datadog.Trace.Iast.Iast(iastSettings);

            IastModule.OnWeakRandomness("fake"); // Add fake span
            var tracer = Tracer.Instance;
            var currentSpan = (tracer.ActiveScope as Scope)?.Span;
            var traceContext = currentSpan?.Context?.TraceContext;
            traceContext.EnableIastInRequest();
            var context = traceContext?.IastRequestContext;
            taintedObjects = context.GetTaintedObjects();
        }

        List<string> res = new List<string>();
        for (int x = 0; x < size; x++)
        {
            var p = $"param{x}";
            taintedObjects?.TaintInputString(p, new Source((SourceType)x, $"source{x}", p));
            res.Add(p);
        }

        return res;
    }

    const int Iterations = 100;

    [IterationSetup(Target = nameof(StringConcatBenchmark))]
    public void InitTaintedContextWhenFalse()
    {
        _initTaintedContextFalse = InitTaintedContext(10, false);
    }

    [Benchmark]
    public void StringConcatBenchmark()
    {
        var parameters = _initTaintedContextFalse;
        var list = new List<string>(parameters.Count);
        var arr = parameters.ToArray();
        for (int x = 0; x < Iterations; x++)
        {
            var txt = string.Concat(arr);
            list.Add(string.Concat(x.ToString(), "Select * from users where name in (", txt, ")"));
        }
        System.Diagnostics.Trace.WriteLine($"{list.Count} elements computed");
    }

    [IterationSetup(Target = nameof(StringConcatAspectBenchmark))]
    public void InitTaintedContextWhenTrue()
    {
        _initTaintedContextTrue = InitTaintedContext(10, true);
    }

    [Benchmark]
    public void StringConcatAspectBenchmark()
    {
        var parameters = _initTaintedContextTrue;
        var list = new List<string>(parameters.Count);
        var arr = parameters.ToArray();
        for (int x = 0; x < Iterations; x++)
        {
            var txt = StringAspects.Concat(arr);
            list.Add(StringAspects.Concat(x.ToString(), "Select * from users where name in (", txt, ")"));
        }
        System.Diagnostics.Trace.WriteLine($"{list.Count} elements computed");
    }

    [IterationCleanup]
    public void Cleanup()
    {
        Tracer.Instance?.ActiveScope?.Close();
        Datadog.Trace.Iast.Iast.Instance = null;
    }
}
