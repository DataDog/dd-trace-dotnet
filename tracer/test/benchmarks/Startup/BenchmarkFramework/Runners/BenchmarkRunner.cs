using System.Reflection;
using BenchmarkFramework.Util;

namespace BenchmarkFramework.Runners;

public abstract class BenchmarkRunner<TBenchmarkContainer, TState>
    where TBenchmarkContainer : new()
{
    private List<Benchmark<TState>>? _benchmarks;
    private Benchmark<TState>? _baseline;

    public IEnumerable<BenchmarkIterationResults> RunAll(int warmupIterationCount, int benchmarkIterationCount)
    {
        foreach (var benchmark in GetBenchmarksInternal())
        {
            yield return RunIterations(
                benchmark,
                warmupIterationCount: warmupIterationCount,
                benchmarkIterationCount: benchmarkIterationCount);
        }
    }

    public IEnumerable<IBenchmark> GetBenchmarks()
    {
        return GetBenchmarksInternal().Cast<IBenchmark>();
    }

    private List<Benchmark<TState>> GetBenchmarksInternal()
    {
        if (_benchmarks is not null)
        {
            return _benchmarks;
        }

        var methods = typeof(TBenchmarkContainer).GetMethods()
                                                 .Select(method => (method, attribute: method.GetCustomAttribute<BenchmarkAttribute>()))
                                                 .Where(t => t.attribute is not null)
                                                 .ToList();

        var benchmarks = new List<Benchmark<TState>>(methods.Count);

        foreach (var (method, attribute) in methods)
        {
            var container = new TBenchmarkContainer();

            var benchmark = new Benchmark<TState>(
                attribute!.Description ?? method.Name,
                attribute.IsBaseline,
                method.CreateDelegate<Action<TState>>(container));

            if (benchmark.IsBaseline)
            {
                if (_baseline is null)
                {
                    _baseline = benchmark;
                }
                else
                {
                    throw new InvalidOperationException("Only one benchmark can be marked as baseline.");
                }
            }

            benchmarks.Add(benchmark);
        }

        _benchmarks = benchmarks;
        return benchmarks;
    }

    private BenchmarkIterationResults RunIterations(
        Benchmark<TState> benchmark,
        int warmupIterationCount,
        int benchmarkIterationCount)
    {
        // run warmup iterations
        for (var i = 0; i < warmupIterationCount; i++)
        {
            _ = RunOnce(benchmark.Action);
        }

        var allResults = new double[benchmarkIterationCount];

        for (var i = 0; i < benchmarkIterationCount; i++)
        {
            allResults[i] = RunOnce(benchmark.Action);
        }

        var (keptResults, removedOutliers) = Statistics.FindOutliers(allResults);
        return new BenchmarkIterationResults(benchmark, keptResults, removedOutliers);
    }

    protected abstract double RunOnce(Action<TState> benchmark);
}
