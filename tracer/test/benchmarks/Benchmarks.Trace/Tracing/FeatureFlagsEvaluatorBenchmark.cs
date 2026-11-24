using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
public class FeatureFlagsEvaluatorBenchmark
{
    private FeatureFlagsEvaluator _independent;
    private FeatureFlagsEvaluator _dependent;
    private EvaluationContext _context;

    [GlobalSetup]
    public void Setup()
    {
        _context = new EvaluationContext("benchmark-subject");
        _independent = new FeatureFlagsEvaluator(null, Configuration(includeDependency: false));
        _dependent = new FeatureFlagsEvaluator(null, Configuration(includeDependency: true));
    }

    [Benchmark(Baseline = true)]
    public object IndependentFlag() => _independent.Evaluate("root", ValueType.String, "fallback", _context);

    [Benchmark]
    public object DependentFlag() => _dependent.Evaluate("root", ValueType.String, "fallback", _context);

    private static ServerConfiguration Configuration(bool includeDependency)
    {
        var rootAllocation = Allocation("root", "on");
        if (includeDependency)
        {
            rootAllocation.Rules =
            [
                new Rule(
                [
                    new ConditionConfiguration
                    {
                        FlagEvaluation = new FlagEvaluationConfiguration { Key = "parent" },
                        Operator = ConditionOperator.ONE_OF,
                        Value = new[] { "on" },
                    },
                ]),
            ];
        }

        return new ServerConfiguration
        {
            Flags = new FlagCollection
            {
                ["root"] = Flag("root", rootAllocation),
                ["parent"] = Flag("parent", Allocation("parent", "on")),
            },
        };
    }

    private static Flag Flag(string key, Allocation allocation)
        => new()
        {
            Key = key,
            Enabled = true,
            VariationType = ValueType.String,
            Variations = new Dictionary<string, Variant> { ["on"] = new("on", "on") },
            Allocations = [allocation],
        };

    private static Allocation Allocation(string key, string variant)
        => new()
        {
            Key = key,
            DoLog = false,
            Splits = [new Split { VariationKey = variant, Shards = [] }],
        };
}
