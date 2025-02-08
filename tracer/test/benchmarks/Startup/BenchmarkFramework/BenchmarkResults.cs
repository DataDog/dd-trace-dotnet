namespace BenchmarkFramework;

public readonly record struct BenchmarkResults(
    int Order,
    string Name,
    bool IsBaseline,
    double[] ElapsedTimes,
    List<BenchmarkResults> RemovedOutliers);
