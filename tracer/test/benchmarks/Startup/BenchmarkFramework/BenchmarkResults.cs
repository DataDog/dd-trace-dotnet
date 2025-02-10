namespace BenchmarkFramework;

public readonly record struct BenchmarkResults(
    string Name,
    bool IsBaseline,
    double ElapsedTime,
    List<BenchmarkResults> RemovedOutliers);

public readonly record struct BenchmarkIterationResults(
    IBenchmark Benchmark,
    List<double> KeptResults,
    List<double> RemovedOutliers);
