#if !NETFRAMEWORK
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 23, phase: 2, templateStr: "NullByRefLocal_Line")]
    [LogLineProbeTestData(lineNumber: 31, phase: 2, templateStr: "NullByRefArg_Line")]
    public class NullByRefArgAndLocal : IRun
    {
        public void Run()
        {
            TriggerNullByRefLocal(0);
            TriggerNullByRefArg();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(phase: 1, templateStr: "NullByRefLocal")]
        private static void TriggerNullByRefLocal(int _)
        {
            var empty = System.Span<byte>.Empty;
            ref byte nullByRef = ref MemoryMarshal.GetReference(empty);
            Consume(ref nullByRef);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TriggerNullByRefArg()
        {
            var empty = System.Span<byte>.Empty;
            ref byte nullByRef = ref MemoryMarshal.GetReference(empty);
            MethodWithNullByRefArg(ref nullByRef);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(phase: 1, templateStr: "NullByRefArg")]
        private static void MethodWithNullByRefArg(ref byte arg)
        {
            Consume(ref arg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Consume(ref byte _)
        {
        }
    }
}
#endif
