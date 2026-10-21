using System.Runtime.CompilerServices;

namespace ReadyToRunInliner.Lib
{
    public static class InstrumentedAtModuleLoad
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Caller(int value) => Target(value) + 1;

        // Small enough for the ReadyToRun compiler to inline into Caller.
        public static int Target(int value) => value * 2;
    }
}
