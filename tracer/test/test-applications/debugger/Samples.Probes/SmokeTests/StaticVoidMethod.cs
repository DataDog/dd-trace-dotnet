using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class StaticVoidMethod : IRun
    {
        public static int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData("System.Void", new string[0])]
        public static void Method()
        {
            Number = 7;
        }
    }
}
