using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    internal class GreaterThanFieldAsync : IAsyncRun
    {
        private int _field;
        private const string Json = @"{
    ""gt"": [
      {""ref"": ""_field""},
      6
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
            evaluateAt: Const.Exit)]
        public async Task<string> Method(int intArg)
        {
            await Task.Delay(20);

            var result = intArg + _field;
            Console.WriteLine(result);
            _field += result;
            return $"Field is: {_field}";
        }
    }
}
