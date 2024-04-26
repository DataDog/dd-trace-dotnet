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
        [LogMethodProbeTestData]
        public unsafe void MethodWithPinnedLocal()
        {
            fixed (char* p = "hello")
            {
                Console.WriteLine(*p);
            }
        }
    }
}
