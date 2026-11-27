using System.Runtime.CompilerServices;

namespace Samples.Probes.Unreferenced.External
{
    public static class NGenInlinedTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Caller(int value)
        {
            return Target(value) + 1;
        }

        public static int Target(int value)
        {
            return value * 2;
        }
    }
}
