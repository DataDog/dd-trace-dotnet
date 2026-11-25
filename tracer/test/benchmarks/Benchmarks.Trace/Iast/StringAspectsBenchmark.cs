// <copyright file="StringAspectsBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.BenchmarkDotNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Aspects.System;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Security.Unit.Tests.Iast;


namespace Benchmarks.Trace.Iast;

[MemoryDiagnoser]
[BenchmarkCategory(Constants.AppSecCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
[IgnoreProfile]
[IterationTime(500)]
public class StringAspectsBenchmark
{
    private const int TimeoutMicroSeconds = 1_000_000;

    private string[] _initTaintedContextTrue;
    private string[] _initTaintedContextFalse;

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
            Datadog.Trace.Iast.Iast.Instance = new Datadog.Trace.Iast.Iast(iastSettings, NullDiscoveryService.Instance);

            IastModule.OnWeakRandomness("fake", false); // Add fake span
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

    [GlobalSetup]
    public void GlobalSetup()
    {
        TracerHelper.SetGlobalTracer();

        _initTaintedContextFalse = InitTaintedContext(10, false).ToArray();
        _initTaintedContextTrue = InitTaintedContext(10, true).ToArray();
    }

    [Benchmark]
    public string StringConcatBenchmark()
    {
        var parameters = _initTaintedContextFalse;
        var txt = string.Concat(parameters);
        return string.Concat("42", "Select * from users where name in (", txt, ")");
    }

    [Benchmark]
    public string StringConcatAspectBenchmark()
    {
        var parameters = _initTaintedContextTrue;
        var txt = StringAspects.Concat(parameters);
        return StringAspects.Concat("42", "Select * from users where name in (", txt, ")");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Tracer.Instance?.ActiveScope?.Close();
        Datadog.Trace.Iast.Iast.Instance = null;
    }
}
