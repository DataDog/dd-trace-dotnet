using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class EmptyCtorTest : IRun
    {
        [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public EmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new EmptyCtorTest();
        }
    }
}
