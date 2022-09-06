using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class NonEmptyCtorTest : IRun
    {
        private Person rnd = new Person("Ashur Thokozani", 35, new Address(), Guid.Empty, null);

        [MethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public NonEmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new NonEmptyCtorTest();
        }
    }
}
