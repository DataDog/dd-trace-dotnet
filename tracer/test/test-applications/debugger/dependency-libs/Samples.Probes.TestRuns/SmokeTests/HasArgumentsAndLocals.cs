using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasArgumentsAndLocals : IRun
    {
        public string FirstName { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method("Last");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            FirstName = "First";
            return FirstName + " " + lastName;
        }
    }
}
