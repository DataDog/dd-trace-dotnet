namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(10)]
    [LogLineProbeTestData(11)]
    [LogLineProbeTestData(12)]
    public class ByRefLikeTest : IRun
    {
        public void Run()
        {
            var byRefLike = new ByRefLike(nameof(ByRefLikeTest));
            byRefLike.CallMe("Hello from the outside 1!", byRefLike, ref byRefLike);
            byRefLike.CallMe2("Hello from the outside 2!", byRefLike, ref byRefLike);
            byRefLike.CallMe3("Hello from the outside 3!", byRefLike, ref byRefLike);
        }

        ref struct ByRefLike
        {
            private string _whoAmI;

            public ByRefLike(string whoAmI)
            {
                _whoAmI = whoAmI;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public ref ByRefLike CallMe(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return ref refByRefLike;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public ByRefLike CallMe2(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return byRefLike;
            }

            [LogMethodProbeTestData(expectedNumberOfSnapshots: 0 /* byref-like is not supported for now */, expectProbeStatusFailure: true)]
            public string CallMe3(string @in, ByRefLike byRefLike, ref ByRefLike refByRefLike)
            {
                return @in + "Hello World";
            }
        }
    }
}
