using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.ExpressionTests
{
    public class AsyncTemplateLocalExitFullSnapshot : IAsyncRun
    {
        private int _field;
        private const string Json = @"{
      ""ref"": ""local""
}";

        public async Task RunAsync()
        {
            _field = 5;
            await Method(_field);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData(
            templateJson: Json,
            captureSnapshot: true,
            evaluateAt: Const.Exit)]
        public async Task<string> Method(int intArg)
        {
            await Task.Delay(20);

            var local = intArg + intArg.ToString().Length;
            Console.WriteLine(local);
            return $"Local is: {local}";
        }
    }
}
