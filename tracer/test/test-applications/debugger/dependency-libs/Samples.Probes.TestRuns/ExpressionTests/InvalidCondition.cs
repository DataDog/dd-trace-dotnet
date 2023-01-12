using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class InvalidCondition : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""undefined > 2""
}";

        private const string Json = @"{
    ""gt"": [
       ""undefined"",
      2
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(1);
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
