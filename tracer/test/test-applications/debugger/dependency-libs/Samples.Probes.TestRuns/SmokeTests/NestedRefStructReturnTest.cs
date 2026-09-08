#if NET6_0_OR_GREATER

using System.Runtime.CompilerServices;
#if NET9_0_OR_GREATER
using System.Threading;
#endif
using Samples.Probes.External;

namespace Samples.Probes.TestRuns.SmokeTests
{
    // Nested byref-like returns must reject the rewrite. EndMethod<TReturn> cannot
    // instantiate a managed generic with a ref struct (GitHub issue 9132).
    public class NestedRefStructReturnTest : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var nested = Nested(21);
            _ = nested.Value;
#if NET9_0_OR_GREATER
            using var scope = LockScope();
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
        public NestedRefLikeContainer.NestedRefLike Nested(int input)
        {
            return new NestedRefLikeContainer.NestedRefLike(input);
        }

#if NET9_0_OR_GREATER
        private static readonly Lock Gate = new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData(expectedNumberOfSnapshots: 0, expectProbeStatusFailure: true)]
        public Lock.Scope LockScope()
        {
            return Gate.EnterScope();
        }
#endif
    }
}

#endif
