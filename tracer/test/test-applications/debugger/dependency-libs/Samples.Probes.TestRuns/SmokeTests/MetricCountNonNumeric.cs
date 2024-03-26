using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricCountNonNumeric : IRun
    {
        private const string Json = @"{""ref"": ""stringArg""}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("qwerty");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MetricMethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Count,
            metricName: nameof(MetricCountNonNumeric),
            captureSnapshot: false,
            expectedNumberOfSnapshots: 1, // error message
            evaluateAt: Const.Exit,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.String" })]
        public string Method(string stringArg)
        {
            return $"Argument: {stringArg}";
        }
    }
}
