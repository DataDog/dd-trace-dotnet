using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(18)]
    internal class InstanceMethodWithArguments : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "System.String" }, skip: true /* Will be returned in the next PR - fix an issue when putting method probe and line probe one same method */)]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
