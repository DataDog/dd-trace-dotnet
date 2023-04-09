using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class AsyncFieldArgumentEntryFullSnapshot : IAsyncRun
    {
        private int _field;

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
      2
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
        [LogMethodProbeTestData(
            conditionJson: Json,
            captureSnapshot: true,
            evaluateAt: Const.Entry)]
        public async Task<string> Method(int intArg)
        {
            await Task.Delay(20);

            var local = _field - intArg;
            Console.WriteLine(local);
            return $"Local is: {local}, Arg is:{intArg}, Field is: {_field}";
        }
    }
}
