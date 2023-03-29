using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogOnLineProbeTestData(18)]
    public class StaticMethodWithArguments : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogOnMethodProbeTestData("System.String", new[] { "System.String" })]
        public static string Method(string lastName)
        {
            return lastName;
        }
    }
}
