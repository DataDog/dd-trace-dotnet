using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 53)]
    public class MultiProbeWithSpanTest : IRun
    {
        private const string MetricJson = @"{""ref"": ""intArg""}";

        private const string LogJson = @"{
    ""gt"": [
      {""ref"": ""myAwesomeLocal""},
      5
    ]
}";

        private const string LogJson2 = @"{
    ""lt"": [
      {""ref"": ""intArg""},
      {""ref"": ""myAwesomeLocal""}
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
        [SpanOnMethodProbeTestData]
        [LogMethodProbeTestData(
            conditionJson: LogJson,
            captureSnapshot: true,
            evaluateAt: "Exit")]
        [LogMethodProbeTestData(
            conditionJson: LogJson2,
            captureSnapshot: false,
            evaluateAt: "Exit")]
        public string Method(int intArg)
        {
            var myAwesomeLocal = intArg * 2;
            PingPong(myAwesomeLocal);
            return $"Argument: {intArg}, {myAwesomeLocal}";
        }

        T PingPong<T>(T t) => t;
    }
}
