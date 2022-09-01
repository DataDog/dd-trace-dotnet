using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.SmokeTests
{
    internal class NonEmptyCtorTest : IRun
    {
        private Random rnd = new Random(100);

        [MethodProbeTestData]
        public NonEmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new NonEmptyCtorTest();
        }
    }
}
