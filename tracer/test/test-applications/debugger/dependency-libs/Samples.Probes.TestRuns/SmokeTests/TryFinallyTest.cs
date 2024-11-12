using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(23)]
    internal class TryFinallyTest : IRun
    {
        public void Run()
        {
            Test(nameof(TryFinallyTest));
        }

        private int Test(string arg)
        {
            int ret;

            try
            {
                ret = arg.Length;
            }
            finally
            {
                ret = arg.Length / 2;
            }

            return ret;
        }
    }
}
