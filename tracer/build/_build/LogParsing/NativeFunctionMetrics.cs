using System;

namespace LogParsing;

public record NativeFunctionMetrics(DateTimeOffset Timestamp, string Name, double TimeMs, int? Count)
{
    public const string TotalInitializationTime = "__total_initialization_time__";
    public double MeanDurationMs => Count is > 0 ? TimeMs / Count.Value : 0;
    public static bool IsTotalName(string name) => name == TotalInitializationTime;
}
