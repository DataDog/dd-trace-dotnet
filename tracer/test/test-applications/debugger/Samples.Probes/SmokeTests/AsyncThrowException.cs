using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncThrowException : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Run(async () => { await Method(nameof(RunAsync)); });
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        private async Task<int> Method(string caller)
        {
            await Task.Delay(20);
            throw new InvalidOperationException($"Exception from {caller}.{nameof(Method)}");
        }
    }
}
