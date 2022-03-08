using System.Runtime.CompilerServices;

namespace Samples.Probes.SmokeTests
{
    internal class HasReturnValue : IRun
    {
        public int Number { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            Method();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        // https://datadoghq.atlassian.net/browse/DEBUG-722
        [MethodProbeTestData("System.String", new string[0], true)]
        public string Method()
        {
            Number = 7;
            return Number.ToString();
        }
    }
}
