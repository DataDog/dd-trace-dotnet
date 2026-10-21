using System.Runtime.CompilerServices;

namespace ReadyToRunInliner
{
    public static class InstrumentedAfterModuleLoad
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Caller(int value) => Target(value) + 1;

        // Small enough for the ReadyToRun compiler to inline into Caller.
        public static int Target(int value) => value * 2;
    }
}
