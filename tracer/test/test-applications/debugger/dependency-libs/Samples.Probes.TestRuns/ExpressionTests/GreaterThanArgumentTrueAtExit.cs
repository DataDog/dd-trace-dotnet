using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanArgumentTrueAtExit : IRun
    {
        private const string Json = @"{
    ""gt"": [
      {""ref"": ""intArg""},
      2
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: Const.Exit,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Argument: {intArg}";
        }
    }
}
