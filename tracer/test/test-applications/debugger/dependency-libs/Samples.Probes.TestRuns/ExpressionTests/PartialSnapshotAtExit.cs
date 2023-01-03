using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class PartialSnapshotAtExit : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""^intArg \u003e 2""
}";

        private const string Json = @"{
  ""json"": {
    ""gt"": [
      ""^intArg"",
      2
    ]
  }
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [ExpressionProbeTestData(
            conditionDsl: Dsl,
            conditionJson: Json,
            isFullSnapshot: false,
            evaluateAt: 1,
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Dsl: {Dsl}, Argument: {intArg}";
        }
    }
}
