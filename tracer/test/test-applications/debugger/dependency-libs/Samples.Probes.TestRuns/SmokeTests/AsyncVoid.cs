using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncVoid : IAsyncRun
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        private async Task VoidTaskMethod()
        {
            try
            {
                await Task.Delay(20);
                var methodName = nameof(VoidTaskMethod);
                await Task.Run(() => { VoidMethod(methodName); });
            }
            catch (AccessViolationException e)
            {
                if (e.Message.Contains("Something"))
                {
                    Console.WriteLine(e.Message);
                }
                else
                {
                    Console.WriteLine(e);
                }
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(skip: true /* Non Deterministic snapshot */)]
        private async void VoidMethod(string caller)
        {
            await Task.Delay(20);
            Console.WriteLine($"{nameof(VoidMethod)} is called from {caller}");
        }
    }
}
