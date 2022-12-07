using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LineProbeTestData(18)]
    public class StaticMethodWithArguments : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [MethodProbeTestData("System.String", new[] { "System.String" })]
        public static string Method(string lastName)
        {
            return lastName;
        }
    }
}
