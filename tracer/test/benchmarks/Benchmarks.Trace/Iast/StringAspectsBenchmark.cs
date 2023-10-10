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

    public IEnumerable<int> IastEnabledContext()
    {
        yield return InitTaintedContext(10);
        //yield return InitTaintedContext(20);
        //yield return InitTaintedContext(100);
    }

    public IEnumerable<int> IastDisabledContext()
    {
        yield return 10;
        //yield return InitTaintedContext(20);
        //yield return InitTaintedContext(100);
    }


    /// <summary>
    /// Generates dummy arguments for the waf
    /// </summary>
    /// <param name="nestingDepth">Encoder.cs respects WafConstants.cs limits to process arguments with a max depth of 20 so above depth 20, there shouldn't be much difference of performances.</param>
    /// <param name="withAttack">an attack present in arguments can slow down waf's run</param>
    /// <returns></returns>
    private static int InitTaintedContext(int size)
    {
        System.Diagnostics.Debugger.Break();

        var settings = new CustomSettingsForTests(new Dictionary<string, object>()
        {
            { ConfigurationKeys.Iast.RequestSampling, 100 },
            { ConfigurationKeys.Iast.Enabled, true }
        });
        var iastSettings = new IastSettings(settings, NullConfigurationTelemetry.Instance);
        Datadog.Trace.Iast.Iast.Instance = new Datadog.Trace.Iast.Iast(iastSettings);

        var tracer = Tracer.Instance;
        System.Diagnostics.Debug.Assert(tracer != null, "Tracer is NULL");
        var iast = Datadog.Trace.Iast.Iast.Instance;
        System.Diagnostics.Debug.Assert(iast != null, "IastInstance is NULL");
        var context = IastModule.GetIastContext();
        System.Diagnostics.Debug.Assert(context != null, "IastContext is NULL");
        var taintedObjects = context.GetTaintedObjects();
        System.Diagnostics.Debug.Assert(taintedObjects != null, "TaintedObjects is NULL");

        taintedObjects.TaintInputString("param1", new Source(1, "kk", "kk"));

        return size;
    }

    [Benchmark]
    [ArgumentsSource(nameof(IastDisabledContext))]
    public void RunStringBenchmark(int size)
    {
        var parameters = new string[] { "param1", "param2", "param3" };
        for (int x = 0; x < 1000; x++)
        {
            var txt = "Select * from users where name in (" + string.Join(", ", parameters) + ")";
        }
    }

    [Benchmark]
    [ArgumentsSource(nameof(IastEnabledContext))]
    public void RunStringAspectBenchmark(int size) 
    {
        var parameters = new string[] { "param1", "param2", "param3" };
        for (int x = 0; x < 1000; x++)
        {
            var txt = "Select * from users where name in (" + string.Join(", ", parameters) + ")";
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        Datadog.Trace.Iast.Iast.Instance = null;
    }
}
