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
            var enumerator = CreateSpan(input).GetEnumerator();
            var fromSpan = enumerator.MoveNext() ? enumerator.Current : 0;
            return nested.Value + fromSpan;
        }

        // Keep the Span backing store out of the probed method so Windows and Linux
        // do not disagree on a named int[] local at method exit.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Span<int> CreateSpan(int input)
        {
            return new[] { input };
        }
    }
}

#endif
