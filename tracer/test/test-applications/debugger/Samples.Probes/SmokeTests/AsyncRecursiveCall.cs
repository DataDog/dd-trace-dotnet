using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class AsyncRecursiveCall : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public async Task RunAsync()
        {
            await Recursive(0);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData(expectedNumberOfSnapshots:2)]
        public async Task<int> Recursive(int i)
        {
            if (i == 1)
            {
                return i + 1;
            }

            return await Recursive(++i);
        }
    }
}
