using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    internal class RecursionWithInnerRefStructTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Recursive(10);
        }

        [LogMethodProbeTestData(expectedNumberOfSnapshots: 11)]
        public async Task<int> Recursive(int iterations)
        {
            if (iterations <= 0)
            {
                return int.MaxValue;
            }

            await Task.Yield();
            return await Deeper.PingPonged.Me(async (int iteration) => await Recursive(iteration), iterations - 1);
        }

        class Deeper
        {
            internal ref struct PingPonged
            {
                [LogMethodProbeTestData(expectedNumberOfSnapshots: 10)]
                public static async Task<int> Me(Func<int, Task<int>> method, int iteration)
                {
                    await Task.Yield();
                    return await method(iteration);
                }
            }
        }
    }
}
