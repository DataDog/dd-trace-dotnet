using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    [ExceptionReplayTestData(expectedNumberOfSnapshotsDefault: 5, expectedNumberOfSnaphotsFull: 35)]
    internal class RecursiveExceptionTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Recursive(10);
        }

        public async Task<int> Recursive(int iterations)
        {
            if (iterations <= 0)
            {
                throw new AbandonedMutexException($"The depth of iterations reached {iterations}");
            }

            await Task.Yield();
            return await PingPonged.Me(async (int iteration) => await Recursive(iteration), iterations - 1);
        }

        ref struct PingPonged
        {
            public static async Task<int> Me(Func<int, Task<int>> method, int iteration)
            {
                return await method(iteration);
            }
        }
    }
}
