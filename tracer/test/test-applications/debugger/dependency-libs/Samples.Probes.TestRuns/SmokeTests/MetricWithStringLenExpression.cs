using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class MetricWithStringLenExpression : IRun
    {
        private const string Json = @"{""len"": {""ref"": ""stringArg""}}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("answer");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MetricMethodProbeTestData(
            metricJson: Json,
            metricKind: Const.Count,
            metricName: nameof(MetricWithStringLenExpression),
            captureSnapshot: false,
            expectedNumberOfSnapshots: 0,
            evaluateAt: Const.Exit,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.String" })]
        public string Method(string stringArg)
        {
            return $"Argument: {stringArg}";
        }
    }
}
