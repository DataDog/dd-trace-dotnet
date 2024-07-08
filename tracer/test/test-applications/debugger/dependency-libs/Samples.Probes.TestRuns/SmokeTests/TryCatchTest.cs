using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(27)]
    [LogLineProbeTestData(31)]
    [LogLineProbeTestData(32)]
    [LogLineProbeTestData(35)]
    internal class TryCatchTest : IRun
    {
        public void Run()
        {
            Test(nameof(TryCatchTest));
        }

        private int Test(string arg)
        {
            var ret1 = 0;
            var ret2 = 0;

            try
            {
                ret1 = arg.Length / 0;
            }
            catch
            {
                ret1 = arg.Length / 2;
                ret2 = ret1 / 3;
            }

            return ret1 + ret2;
        }
    }
}
