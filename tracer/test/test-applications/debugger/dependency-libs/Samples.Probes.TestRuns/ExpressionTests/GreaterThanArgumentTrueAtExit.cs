using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanArgumentTrueAtExit : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""ref intArg > 2""
}";

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
        [MethodProbeTestData(
            conditionDsl: Dsl,
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: 1,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Argument: {intArg}";
        }
    }
}
