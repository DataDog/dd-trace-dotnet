using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class HasException : IRun
    {
        public string Name { get; set; } = "A";

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData("System.Int32", new[] { "System.String" }, true)]
        public int Method(string name)
        {
            return int.Parse(name);
        }
    }
}
