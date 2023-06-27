using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    // Phase 1
    [LogLineProbeTestData(lineNumber: 79, phase: 1)]
    [LogLineProbeTestData(lineNumber: 81, phase: 1)]
    [LogLineProbeTestData(lineNumber: 82, phase: 1)]

    // Phase 2
    [LogLineProbeTestData(lineNumber: 79, phase: 2)]
    [LogLineProbeTestData(lineNumber: 84, phase: 2, expectedNumberOfSnapshots: 6)]
    [LogLineProbeTestData(lineNumber: 87, phase: 2, expectedNumberOfSnapshots: 3)]
    [LogLineProbeTestData(lineNumber: 90, phase: 2, expectedNumberOfSnapshots: 6)]
    [LogLineProbeTestData(lineNumber: 93, phase: 2)]
    [LogLineProbeTestData(lineNumber: 104, phase: 2, expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]

    // Phase 3
    [LogLineProbeTestData(lineNumber: 93, phase: 3)]
    [LogLineProbeTestData(lineNumber: 99, phase: 3)]

    // Phase 4
    [LogLineProbeTestData(lineNumber: 101, phase: 4)]

    // Phase 5
    [LogLineProbeTestData(lineNumber: 105, phase: 5)]

    // Phase 6
    [LogLineProbeTestData(lineNumber: 94, phase: 6, expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
    public class MethodWithMultiplePhasingAndProbes
    {
        private const string Json = @"{""ref"": ""a""}";
        private const string ConditionEvaluatesToFalseJson = @"{
    ""gt"": [
      {""ref"": ""num""},
      {""ref"": ""a""}
    ]
}";
        private const string ConditionEvaluatesToTrueJson = @"{
    ""lt"": [
      {""ref"": ""num""},
      {""ref"": ""a""}
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Acc(nameof(Acc), 2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 1, phase: 1)]
        [MetricMethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Count,
            metricName: "NonAsyncAccMetric",
            expectedNumberOfSnapshots: 0,
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            phase: 1)]
        [SpanOnMethodProbeTestData(phase: 1)]

        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, conditionJson: ConditionEvaluatesToFalseJson, evaluateAt: Const.Exit, phase: 2)]
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 1, conditionJson: ConditionEvaluatesToTrueJson, evaluateAt: Const.Exit, phase: 2)]
        [MetricMethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Count,
            metricName: "NonAsyncAccMetric",
            expectedNumberOfSnapshots: 0,
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            phase: 2)]
        [SpanOnMethodProbeTestData(phase: 2)]
        public int Acc(string calleeName, int num)
        {
            int a = calleeName.Length * num;

            int sum = a;
            for (int i = 2; i < a + 2; i++)
            {
                if (i > (a + 2) / 2)
                {
                    // Passed half the iterations
                    sum /= 3;
                }

                sum *= i;
            }

            if (sum < 10)
            {
                // Unreachable branch
                sum = 0;
            }

            a++;
            a++;
            a++;
            a++;
            a++;

            return a;
        }
    }
}
