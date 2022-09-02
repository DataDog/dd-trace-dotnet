using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncThrowExceptionAsyncVoid : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Task.Run(() => { VoidMethod(nameof(RunAsync)); });
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(skip:true)]
        private async void VoidMethod(string caller)
        {
            await Task.Delay(20);
            throw new InvalidOperationException($"Exception from {caller}.{nameof(VoidMethod)}");
        }
    }
}
