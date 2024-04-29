using System;
using System.Runtime.CompilerServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    internal class PinnedLocal : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            MethodWithPinnedLocal();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(skipOnFrameworks: new[] { "net462", "netcoreapp2.1"})]
        public unsafe void MethodWithPinnedLocal()
        {
            fixed (char* p = "hello")
            {
                Console.WriteLine(*p);
            }
        }
    }
}
