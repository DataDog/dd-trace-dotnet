using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class FieldSmallerThanLocalAsync : IAsyncRun
    {
        private int _field;
        private const string Dsl = @"{
  ""dsl"": ""ref _field < ref local""
}";

        private const string Json = @"{
  ""json"": {
    ""gt"": [
      {""ref"": ""_field""},
      {""ref"": ""local""}
    ]
  }
}";

        public async Task RunAsync()
        {
            _field = 5;
            await Method(_field);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            conditionDsl: Dsl,
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: 1)]
        public async Task<string> Method(int intArg)
        {
            await Task.Delay(20);

            var local = intArg + intArg.ToString().Length;
            Console.WriteLine(local);
            return $"Local is: {local}";
        }
    }
}
