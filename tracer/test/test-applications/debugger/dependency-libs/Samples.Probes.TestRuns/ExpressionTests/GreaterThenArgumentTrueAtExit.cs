using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThenArgumentTrueAtExit : IRun
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
        [ExpressionProbeTestData(Dsl, Json, isCondition: true, evaluateAt: 1, "System.String", new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            return $"Dsl: {Dsl}, Argument: {intArg}";
        }
    }
}
