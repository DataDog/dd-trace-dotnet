using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    public class StaticType : IRun
    {
        public void Run()
        {
            StaticTypeInner.Method("Last name");
        }

        public static class StaticTypeInner
        {
            public static string _staticField = "Static Field";
            public static string StaticProperty { get; } = "Static Property";

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "System.String" })]
            public static string Method(string lastName)
            {
                return lastName;
            }
        }
    }
}
