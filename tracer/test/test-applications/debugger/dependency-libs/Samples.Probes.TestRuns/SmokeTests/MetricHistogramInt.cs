using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricHistogramInt : IRun
    {
        private const string Json = @"{""ref"": ""intArg""}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MetricOnMethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Histogram,
            metricName: nameof(MetricHistogramInt),
            captureSnapshot: false,
            evaluateAt: Const.Exit,
            expectedNumberOfSnapshots: 0,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Argument: {intArg}";
        }
    }
}
