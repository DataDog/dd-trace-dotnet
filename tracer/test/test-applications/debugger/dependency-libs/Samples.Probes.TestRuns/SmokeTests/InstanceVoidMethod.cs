using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class InstanceVoidMethod : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.Void", new string[0])]
        public void Method()
        {
            Number = 7;
        }
    }
}
