using System;

namespace StartupBenchmarks;

public readonly record struct BenchmarkResults(
    string Name,
    bool IsBaseline,
    TimeSpan ElapsedToMain,
    TimeSpan ElapsedToExit);
