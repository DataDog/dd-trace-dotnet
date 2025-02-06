namespace StartupBenchmarks;

public readonly record struct BenchmarkResults(
    int Order,
    string Name,
    bool IsBaseline,
    double[] ElapsedTimes);
