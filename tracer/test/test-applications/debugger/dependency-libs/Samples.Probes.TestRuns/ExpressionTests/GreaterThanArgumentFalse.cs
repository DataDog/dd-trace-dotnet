using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanArgumentFalse : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""ref intArg > 2""
}";

        private const string Json = @"{
  ""json"": {
    ""gt"": [
      {""ref"": ""intArg""},
      2
    ]
  }
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [ExpressionProbeTestData(conditionDsl: Dsl,
                                 conditionJson: Json,
                                 captureSnapshot: true,
                                 evaluateAt: 1,
                                 expectedNumberOfSnapshots: 0,
                                 returnTypeName: "System.String",
                                 parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Argument: {intArg}";
        }
    }
}
