using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class InstanceVoidMethod : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Void", new string[0])]
        public void Method()
        {
            Number = 7;
        }
    }
}
