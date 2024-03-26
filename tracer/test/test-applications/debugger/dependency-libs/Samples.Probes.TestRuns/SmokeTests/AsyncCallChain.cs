using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class AsyncCallChain : IAsyncRun
    {
        private int _chain;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            _chain++;
            await Async1(_chain);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<int> Async1(int chain)
        {
            chain++;
            var result = await Async2(chain);
            if (result > chain.ToString().Length + 10)
            {
                return result - 2;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<int> Async2(int chain)
        {
            await Task.Delay(20);
            chain++;
            return chain;
        }
    }
}
