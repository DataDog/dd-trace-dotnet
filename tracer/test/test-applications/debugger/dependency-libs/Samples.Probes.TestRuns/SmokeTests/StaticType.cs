using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LineProbeTestData(lineNumber: 23)]
    public class StaticType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            StaticTypeInner.Method("Last name");
        }

        public static class StaticTypeInner
        {
            public static string _staticField = "Static Field";
            public static string StaticProperty { get; } = "Static Property";

            [MethodImpl(MethodImplOptions.NoInlining)]
            [MethodProbeTestData("System.String", new[] { "System.String" })]
            public static string Method(string lastName)
            {
                return lastName;
            }
        }
    }
}
