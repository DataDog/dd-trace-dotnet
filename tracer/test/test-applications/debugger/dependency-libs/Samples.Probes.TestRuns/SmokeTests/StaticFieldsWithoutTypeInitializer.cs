using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class StaticFieldsWithoutTypeInitializer : IRun
    {
        private static string _staticField;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            _staticField = "Static Field";
            Method("Last name");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.String", new[] { "System.String" })]
        public string Method(string lastName)
        {
            return lastName;
        }
    }
}
