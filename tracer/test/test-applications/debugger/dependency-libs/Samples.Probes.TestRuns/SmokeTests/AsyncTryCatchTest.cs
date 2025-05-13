using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(28, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(32, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(33, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(34, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(38, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(39, skipOnFrameworks: ["net48", "net462"])]
    [LogLineProbeTestData(40, skipOnFrameworks: ["net48", "net462"])]
    internal class AsyncTryCatchTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Test(nameof(AsyncTryCatchTest));
        }

        [LogMethodProbeTestData(skipOnFrameworks: ["net48", "net462"])]
        private async Task<int> Test(string arg)
        {
            int ret;

            await Task.Yield();

            try
            {
                await Task.Yield();
                ret = arg.Length;
                await Task.Yield();
            }
            finally
            {
                await Task.Yield();
                ret = arg.Length / 2;
                await Task.Yield();
            }

            await Task.Yield();
            return ret;
        }
    }
}
