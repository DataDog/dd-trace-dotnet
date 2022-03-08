using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class StaticVoidMethod : IRun
    {
        public static int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        // https://datadoghq.atlassian.net/browse/DEBUG-722
        [MethodProbeTestData("System.Void", new string[0], true)]
        public static void Method()
        {
            Number = 7;
        }
    }
}
