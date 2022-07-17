using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncMethodInsideTaskRun : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await RunInsideTask();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<string> RunInsideTask()
        {
            return await Task.Run(
                 async () =>
                 {
                     var local1 = $"{nameof(RunInsideTask)}: Start";
                     var res = await Method(local1.Substring(0, nameof(RunInsideTask).Length));
                     return res + ": Finished";
                 });
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<string> Method(string seed)
        {
            await Task.CompletedTask;
            string result = seed + " ";
            for (int i = 0; i < 5; i++)
            {
                result += i;
            }

            return result;
        }
    }
}
