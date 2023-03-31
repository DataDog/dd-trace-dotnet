using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasReturnValue : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new string[0])]
        public string Method()
        {
            Number = 7;
            return Number.ToString();
        }
    }
}
