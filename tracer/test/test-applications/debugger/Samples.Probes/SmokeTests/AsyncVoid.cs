using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncVoid: IAsyncRun
    {
        public async Task RunAsync()
        {
            try
            {
                await VoidTaskMethod();
            }
            catch (AccessViolationException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        private async Task VoidTaskMethod()
        {
            try
            {
                await Task.CompletedTask;
                var methodName = nameof(VoidTaskMethod);
                await Task.Run(() => {VoidMethod(methodName);});
            }
            catch (AccessViolationException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        private async void VoidMethod(string caller)
        {
            await Task.CompletedTask;
            Console.WriteLine($"{nameof(VoidMethod)} is called from {caller}");
        }
    }
}
