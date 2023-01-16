using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class GreaterThanArgumentAsyncTrue : IAsyncRun
    {
        private const string Dsl = @"{
  ""dsl"": ""ref intArg > 2""
}";

        private const string Json = @"{
    ""gt"": [
      {""ref"": ""blabla""},
      2
    ]
}";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(conditionDsl: Dsl,
                             conditionJson: Json,
                             captureSnapshot: false,
                             evaluateAt: 1)]
        public async Task<string> Method(int intArg)
        {
            await Task.Yield();

            return $"Argument: {intArg}";
        }
    }
}
