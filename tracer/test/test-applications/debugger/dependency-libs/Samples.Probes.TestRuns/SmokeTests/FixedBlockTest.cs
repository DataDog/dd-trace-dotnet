#if NET6_0_OR_GREATER

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Samples.Probes.TestRuns.SmokeTests.MultidimensionalArrayTest;

namespace Samples.Probes.TestRuns.SmokeTests
{
    internal class FixedBlockTest : IRun
    {
        public unsafe void Run()
        {
            Test(5);
        }

        [LogMethodProbeTestData]
        public unsafe void Test(byte a)
        {
            byte[] array = null;
            Span<byte> span2 = new Span<byte>([1, 2, 3, a]);
            try
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(span2))
                {
                    Span<byte> byteSpan = new Span<byte>(ptr, span2.Length);
                }
            }
            finally
            {
                if (array != null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }
    }
}

#endif
