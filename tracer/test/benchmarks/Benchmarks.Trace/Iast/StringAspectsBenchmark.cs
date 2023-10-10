// <copyright file="StringInstrumentationBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.Unit.Tests.Iast;


namespace Benchmarks.Trace.Iast;

[MemoryDiagnoser]
[BenchmarkAgent7]
public class StringAspectsBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    static StringAspectsBenchmark()
    {
    }

    public IEnumerable<List<string>> IastEnabledContext()
    {
        yield return InitTaintedContext(10);
        yield return InitTaintedContext(20);
        yield return InitTaintedContext(100);
    }

    public IEnumerable<List<string>> IastDisabledContext()
    {
        yield return InitTaintedContext(10, false);
        yield return InitTaintedContext(20, false);
        yield return InitTaintedContext(100, false);
    }


    /// <summary>
    /// Generates dummy arguments for the waf
    /// </summary>
    /// <param name="nestingDepth">Encoder.cs respects WafConstants.cs limits to process arguments with a max depth of 20 so above depth 20, there shouldn't be much difference of performances.</param>
    /// <param name="withAttack">an attack present in arguments can slow down waf's run</param>
    /// <returns></returns>
    private static List<string> InitTaintedContext(int size, bool initTainted = true)
    {
        TaintedObjects taintedObjects = null;

        if (initTainted)
        {
            var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 100 },
            { ConfigurationKeys.Iast.Enabled, true }
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
            taintedObjects?.TaintInputString(p, new Source(1, "kk", "kk"));
            res.Add(p);
        }

        return res;
    }

    [Benchmark]
    [ArgumentsSource(nameof(IastDisabledContext))]
    public void RunStringBenchmark(List<string> parameters)
    {
        for (int x = 0; x < 1000; x++)
        {
            var txt = "Select * from users where name in (" + string.Join(", ", parameters) + ")";
        }
    }

    [Benchmark]
    [ArgumentsSource(nameof(IastEnabledContext))]
    public void RunStringAspectBenchmark(List<string> parameters)
    {
        for (int x = 0; x < 1000; x++)
        {
            var txt = StringAspects.Concat("Select * from users where name in (", StringAspects.Join(", ", parameters), ")");
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        Datadog.Trace.Iast.Iast.Instance = null;
    }
}
