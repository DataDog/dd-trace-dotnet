using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 39)]
    public class MultiProbeTest : IRun
    {
        private const string MetricJson = @"{""ref"": ""intArg""}";

        private const string LogJson = @"{
    ""gt"": [
      {""ref"": ""intArg""},
      5
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(6);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MetricMethodProbeTestData(
            metricJson: MetricJson,
            metricKind: Const.Count,
            metricName: nameof(MetricCountInt),
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            expectedNumberOfSnapshots: 0)]
        [LogMethodProbeTestData]
        [LogMethodProbeTestData(
            conditionJson: LogJson,
            captureSnapshot: true,
            evaluateAt: "Exit")]
        public string Method(int intArg)
        {
            return $"Argument: {intArg}";
        }
    }
}
