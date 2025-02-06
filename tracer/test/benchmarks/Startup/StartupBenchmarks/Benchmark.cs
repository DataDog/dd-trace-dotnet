using System;

namespace StartupBenchmarks;

public readonly record struct Benchmark<TState>(
    int Order,
    string Name,
    bool IsBaseline,
    Action<TState> Action);
