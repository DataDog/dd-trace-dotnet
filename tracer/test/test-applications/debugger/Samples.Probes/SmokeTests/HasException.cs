using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class HasException : IRun
    {
        public string Name { get; set; } = "A";

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method(Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.Int32", new[] { "System.String" }, true)]
        public int Method(string name)
        {
            return int.Parse(name);
        }
    }
}
