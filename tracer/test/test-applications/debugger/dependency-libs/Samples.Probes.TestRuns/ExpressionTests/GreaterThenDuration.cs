using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThenDuration : IRun
    {
        private const string Dsl = @"{
  ""dsl"": ""ref @duration > 0""
}";

        private const string Json = @"{
    ""gt"": [
      {""ref"": ""@duration""},
      0
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: "Exit",
            returnTypeName: "System.String",
            parametersTypeName: new[] { "System.Int32" })]
        public string Method(int intArg)
        {
            Console.WriteLine(intArg);
            return $"Arg is: {intArg}";
        }
    }
}
