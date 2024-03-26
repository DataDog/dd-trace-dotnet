using System;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class NonEmptyCtorTest : IRun
    {
        private Person rnd = new Person("Ashur Thokozani", 35, new Address(), Guid.Empty, null);

        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
        public NonEmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new NonEmptyCtorTest();
        }
    }
}
