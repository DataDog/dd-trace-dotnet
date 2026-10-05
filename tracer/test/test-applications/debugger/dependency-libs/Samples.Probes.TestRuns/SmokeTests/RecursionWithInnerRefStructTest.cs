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

        // Both probes are skipped on net11.0: .NET 11 renamed MulticastDelegate's private fields
        // (_invocationList -> _helperObject, _invocationCount -> _extraData, _methodBase removed),
        // so the captured fields of the Func<int, Task<int>> argument of Me() below no longer match
        // the approval. The debugger approvals have no per-framework variant, so the test is skipped
        // until the debugger team decides whether to scrub delegate internals out of snapshots.
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 11, skipOnFrameworks: ["net11.0"])]
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
                // See the comment on Recursive() above.
                [LogMethodProbeTestData(expectedNumberOfSnapshots: 10, skipOnFrameworks: ["net11.0"])]
                public static async Task<int> Me(Func<int, Task<int>> method, int iteration)
                {
                    await Task.Yield();
                    return await method(iteration);
                }
            }
        }
    }
}
