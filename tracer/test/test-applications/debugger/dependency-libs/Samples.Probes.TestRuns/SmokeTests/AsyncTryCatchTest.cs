using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(28)]
    [LogLineProbeTestData(32)]
    [LogLineProbeTestData(33)]
    [LogLineProbeTestData(34)]
    [LogLineProbeTestData(38)]
    [LogLineProbeTestData(39)]
    [LogLineProbeTestData(40)]
    internal class AsyncTryCatchTest : IAsyncRun
    {
        public async Task RunAsync()
        {
            await Test(nameof(AsyncTryCatchTest));
        }

        [LogMethodProbeTestData]
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
