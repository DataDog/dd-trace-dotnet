using System.Reflection;

namespace StartupBenchmarks;

public abstract class BenchmarkRunner<TBenchmarksContainer, TState>
    where TBenchmarksContainer : new()
{
    private List<Benchmark<TState>>? _benchmarks;
    private Benchmark<TState>? _baseline;

    public IEnumerable<BenchmarkResults> RunAll(int iterationCount)
    {
        var benchmarks = GetBenchmarks(out _);

        // run the baseline first
        if (_baseline is not null)
        {
            yield return RunIterations(_baseline.Value, iterations: iterationCount);
        }

        // run the rest of the benchmarks
        foreach (var benchmark in benchmarks.Where(b => !b.IsBaseline))
        {
            yield return RunIterations(benchmark, iterations: iterationCount);
        }
    }

    public List<Benchmark<TState>> GetBenchmarks(out Benchmark<TState>? baseline)
    {
        if (_benchmarks is not null)
        {
            baseline = _baseline;
            return _benchmarks;
        }

        baseline = null;

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

    private BenchmarkResults RunIterations(Benchmark<TState> benchmark, int iterations)
    {
        var allResults = new List<BenchmarkResults>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            allResults.Add(RunOnce(benchmark));
        }

        var elapsedTimesCount = allResults[0].ElapsedTimes.Length;

        if (allResults.Any(r => r.ElapsedTimes.Length != elapsedTimesCount))
        {
            throw new InvalidOperationException("All benchmarks must return the same number of elapsed times.");
        }

        var elapsedTimeAverages = new double[elapsedTimesCount];

        for (var i = 0; i < elapsedTimesCount; i++)
        {
            elapsedTimeAverages[i] = allResults.Average(r => r.ElapsedTimes[i]);
        }

        return new BenchmarkResults(
            benchmark.Order,
            benchmark.Name,
            benchmark.IsBaseline,
            elapsedTimeAverages);
    }

    protected abstract BenchmarkResults RunOnce(Benchmark<TState> benchmark);
}
