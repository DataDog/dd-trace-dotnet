namespace Samples.Probes.TestRuns.SmokeTests
{
    public class CtorTransparentCodeTest : IRun
    {
        public void Run()
        {
            var instance = new Samples.Probes.External.SecurityTransparentTest();
        }
    }
}
