#if NET9_0_OR_GREATER

using System.Runtime.CompilerServices;
using System.Threading;

namespace Samples.Probes.TestRuns.SmokeTests
{
    // lock (System.Threading.Lock) introduces a nested ref struct local (Lock.Scope)
    // type-forwarded from System.Runtime to CoreLib. GitHub issue 9132.
    public class NestedRefStructLockLocalTest : IRun
    {
        private static readonly Lock Gate = new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(21);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public int Method(int input)
        {
            lock (Gate)
            {
                return input * 2;
            }
        }
    }
}

#endif
