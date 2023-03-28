using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricCountString : IRun
    {
        private const string Json = @"{""ref"": ""intArg""}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("2");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MetricOnMethodProbeTestData(
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
