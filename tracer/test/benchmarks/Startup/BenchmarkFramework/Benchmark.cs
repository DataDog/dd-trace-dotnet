namespace BenchmarkFramework;

public interface IBenchmark
{
    string Name { get; init; }

    bool IsBaseline { get; init; }
}

public readonly record struct Benchmark<TState>(
    string Name,
    bool IsBaseline,
    Action<TState> Action)
    : IBenchmark;
