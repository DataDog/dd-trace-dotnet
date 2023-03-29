namespace Samples.Probes.TestRuns.SmokeTests
{
    public class EmptyCtorTest : IRun
    {
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0)]
        public EmptyCtorTest()
        {
        }

        public void Run()
        {
            var @this = new EmptyCtorTest();
        }
    }
}
