using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncCallChain : IAsyncRun
    {
        private int _chain;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            _chain++;
            await Async1(_chain);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<int> Async1(int chain)
        {
            chain++;
            var result = await Async2(chain);
            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData]
        public async Task<int> Async2(int chain)
        {
            await Task.CompletedTask;
            chain++;
            return chain;
        }
    }
}
