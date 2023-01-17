using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class AsyncFieldArgumentLocalExitFullSnapshot : IAsyncRun
    {
       private int _field;
        private const string Dsl = @"{
  ""dsl"": ""ref _field > ref intArg and ref _field > ref local""
}";

        private const string Json = @"{
""or"": [
{
    ""gt"": [
      {""ref"": ""_field""},
      {""ref"": ""intArg""}
    ]
},
{
 ""gt"": [
      {""ref"": ""_field""},
      {""ref"": ""local""}
    ]
}
]
}";

        public async Task RunAsync()
        {
            _field = 5;
            await Method(3);
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

            var local = _field - intArg;
            Console.WriteLine(local);
            return $"Local is: {local}, Arg is:{intArg}, Field is: {_field}";
        }
    }
}
