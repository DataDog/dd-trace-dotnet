using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricGaugeInt : IRun
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
            metricKind: Const.Gauge,
            metricName: nameof(MetricGaugeInt),
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
