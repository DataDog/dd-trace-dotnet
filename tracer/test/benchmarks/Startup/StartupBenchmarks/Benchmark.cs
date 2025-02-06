using System;

namespace StartupBenchmarks;

public interface IBenchmark
{
    int Order { get; init; }
    string Name { get; init; }
    bool IsBaseline { get; init; }
}

public readonly record struct Benchmark<TState>(
    int Order,
    string Name,
    bool IsBaseline,
    Action<TState> Action) : IBenchmark;
