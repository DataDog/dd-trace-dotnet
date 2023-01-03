using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class UndefinedValue : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""^intArg \u003e 2""
}";

        private const string Json = @"{
  ""json"": {
    ""gt"": [
      ""^undefine"",
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
        [ExpressionProbeTestData(Dsl, Json, isCondition: true, evaluateAt: 1, "System.String", new[] { "System.Int32" }, expectedNumberOfSnapshots: 1)]
        public string Method(int intArg)
        {
            return $"Dsl: {Dsl}, Argument: {intArg}";
        }
    }
}
