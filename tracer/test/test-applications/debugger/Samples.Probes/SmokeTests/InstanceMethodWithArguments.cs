using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class InstanceMethodWithArguments : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
