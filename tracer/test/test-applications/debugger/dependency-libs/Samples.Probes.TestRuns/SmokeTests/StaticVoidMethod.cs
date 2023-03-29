using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class StaticVoidMethod : IRun
    {
        public static int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.Void", new string[0])]
        public static void Method()
        {
            Number = 7;
        }
    }
}
