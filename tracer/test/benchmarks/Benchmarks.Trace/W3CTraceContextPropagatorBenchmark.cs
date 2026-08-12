using BenchmarkDotNet.Attributes;
using Datadog.Trace.Propagators;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
public class W3CTraceContextPropagatorBenchmark
{
    private const string SingleVendorTraceState =
        "dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345~";

    private const string MultipleVendorsTraceState =
        "congo=t61rcWkgMzE,dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;" +
        "t.usr.id:12345~,rojo=00f067aa0ba902b7";

    private const string MultipleVendorsWithDatadogFirstTraceState =
        "dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;t.usr.id:12345~," +
        "congo=t61rcWkgMzE,rojo=00f067aa0ba902b7";

    private const string MultipleVendorsWithOtelTraceState =
        "congo=t61rcWkgMzE,dd=s:2;o:rum;p:0123456789abcdef;t.dm:-4;" +
        "t.usr.id:12345~,ot=rv:ef284ace7a91e1;th:e6666666666668," +
        "rojo=00f067aa0ba902b7";

    [Benchmark]
    public string ParseTraceStateWithSingleVendor()
        => W3CTraceContextPropagator.ParseTraceState(SingleVendorTraceState)
                                     .LastParent;

    [Benchmark]
    public string ParseTraceStateWithMultipleVendors()
        => W3CTraceContextPropagator.ParseTraceState(MultipleVendorsTraceState)
                                     .LastParent;

    [Benchmark]
    public string ParseTraceStateWithMultipleVendorsAndDatadogFirst()
        => W3CTraceContextPropagator.ParseTraceState(MultipleVendorsWithDatadogFirstTraceState)
                                     .LastParent;

    [Benchmark]
    public string ParseTraceStateWithMultipleVendorsAndOtelTraceState()
        => W3CTraceContextPropagator.ParseTraceState(MultipleVendorsWithOtelTraceState)
                                     .LastParent;
}
