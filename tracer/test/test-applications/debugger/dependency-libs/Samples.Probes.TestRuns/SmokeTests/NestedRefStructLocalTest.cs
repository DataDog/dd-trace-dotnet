#if NET6_0_OR_GREATER

using System;
using System.Runtime.CompilerServices;
using Samples.Probes.External;

namespace Samples.Probes.TestRuns.SmokeTests
{
    // Nested byref-like locals from another assembly (Span<T>.Enumerator is type-forwarded)
    // must be skipped; LogLocal<TLocal> cannot take ref structs. GitHub issue 9132.
    public class NestedRefStructLocalTest : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            Method(21);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public int Method(int input)
        {
            var nested = new NestedRefLikeContainer.NestedRefLike(input);
            int[] values = { input };
            Span<int> span = values;
            var enumerator = span.GetEnumerator();
            var fromSpan = enumerator.MoveNext() ? enumerator.Current : 0;
            return nested.Value + fromSpan;
        }
    }
}

#endif
