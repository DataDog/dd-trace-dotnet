using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    [LineProbeTestData(lineNumber: 23)]
    public class StaticType : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            StaticTypeInner.Method("Last name");
        }

        public static class StaticTypeInner
        {
            public static string _staticField = "Static Field";
            public static string StaticProperty { get; } = "Static Property";

            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            [MethodProbeTestData("System.String", new[] { "System.String" }, skip: true /* Will be returned in the next PR - fix an issue when putting method probe and line probe one same method */)]
            public static string Method(string lastName)
            {
                return lastName;
            }
        }
    }
}
