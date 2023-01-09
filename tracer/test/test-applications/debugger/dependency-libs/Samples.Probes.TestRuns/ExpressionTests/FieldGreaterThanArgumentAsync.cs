using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class FieldGreaterThanArgumentAsync : IAsyncRun
    {
        private int _field;
        private const string Dsl = @"{
  ""dsl"": ""ref _field > ref intArg""
}";

        private const string Json = @"{
  ""json"": {
    ""gt"": [
      {""ref"": ""_field""},
      {""ref"": ""intArg""}
    ]
  }
}";

        public async Task RunAsync()
        {
            _field = 5;
            await Method(3);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [ExpressionProbeTestData(
            conditionDsl: Dsl,
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: 1)]
        public async Task<string> Method(int intArg)
        {
            await Task.Delay(20);

            var result = intArg + _field;
            Console.WriteLine(result);
            return $"Result is: {result}";
        }
    }
}
