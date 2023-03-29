namespace Samples.Probes.TestRuns.SmokeTests
{
    public class EmptyCtorTest : IRun
    {
        [LogOnMethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public EmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new EmptyCtorTest();
        }
    }
}
