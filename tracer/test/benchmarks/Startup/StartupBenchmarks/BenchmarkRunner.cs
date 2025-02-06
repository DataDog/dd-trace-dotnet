using System.Reflection;

namespace StartupBenchmarks;

public abstract class BenchmarkRunner<TBenchmarksContainer, TState>
    where TBenchmarksContainer : new()
{
    private List<Benchmark<TState>>? _benchmarks;
    private Benchmark<TState>? _baseline;

    public IEnumerable<BenchmarkResults> RunAll(int warmupIterationCount, int benchmarkIterationCount)
    {
        var benchmarks = GetBenchmarksInternal();

        // run the baseline first
        if (_baseline is not null)
        {
            yield return RunIterations(
                _baseline.Value,
                warmupIterationCount: warmupIterationCount,
                benchmarkIterationCount: benchmarkIterationCount);
        }

        // run the rest of the benchmarks
        foreach (var benchmark in benchmarks.Where(b => !b.IsBaseline))
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

        var methods = typeof(TBenchmarksContainer).GetMethods()
                                                  .Select(method => (method, attribute: method.GetCustomAttribute<BenchmarkAttribute>()))
                                                  .Where(t => t.attribute is not null)
                                                  .ToList();

        var benchmarks = new List<Benchmark<TState>>(methods.Count);

        foreach (var (method, attribute) in methods)
        {
            var container = new TBenchmarksContainer();

            var benchmark = new Benchmark<TState>(
                benchmarks.Count,
                attribute!.Description ?? method.Name,
                attribute.IsBaseline,
                (Action<TState>)method.CreateDelegate(typeof(Action<TState>), container));

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

    private BenchmarkResults RunIterations(
        Benchmark<TState> benchmark,
        int warmupIterationCount,
        int benchmarkIterationCount)
    {
        // run once to get the number of elapsed times and start the warmup
        var warmupResults = RunOnce(benchmark);
        var elapsedTimesCount = warmupResults.ElapsedTimes.Length;

        // run the rest of the warmup iterations
        for (var i = 0; i < warmupIterationCount - 1; i++)
        {
            _ = RunOnce(benchmark);
        }

        var allResults = new List<BenchmarkResults>(benchmarkIterationCount);

        for (var i = 0; i < benchmarkIterationCount; i++)
        {
            allResults.Add(RunOnce(benchmark));
        }

        if (allResults.Any(r => r.ElapsedTimes.Length != elapsedTimesCount))
        {
            throw new InvalidOperationException("All benchmarks must return the same number of elapsed time data points.");
        }

        var (keptResults, removedOutliers) = Statistics.FindOutliersBy(allResults, r => r.ElapsedTimes.Sum());

        var elapsedTimeAverages = new double[elapsedTimesCount];

        for (var i = 0; i < elapsedTimesCount; i++)
        {
            elapsedTimeAverages[i] = keptResults.Average(r => r.ElapsedTimes[i]);
        }

        return new BenchmarkResults(
            benchmark.Order,
            benchmark.Name,
            benchmark.IsBaseline,
            elapsedTimeAverages,
            removedOutliers);
    }

    protected abstract BenchmarkResults RunOnce(Benchmark<TState> benchmark);
}
