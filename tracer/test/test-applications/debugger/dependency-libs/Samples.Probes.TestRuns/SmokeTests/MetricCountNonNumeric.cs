using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricCountNonNumeric : IRun
    {
        private const string Json = @"{""ref"": ""intArg""}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("qwerty");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Count,
            metricName: nameof(MetricCountInt),
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            expectedNumberOfSnapshots: 0,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.String" })]
        public string Method(string stringArg)
        {
            return $"Argument: {stringArg}";
        }
    }
}
