#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    /// <summary>
    /// Handwritten MoveNext whose SetException catch does not bind the caught exception.
    /// Roslyn emits <c>pop</c>, rather than <c>stloc</c>, as the first handler instruction.
    /// Async method and span probes must insert only after that instruction has consumed the exception.
    /// </summary>
    public class CatchPopFirstInstructionTest : IAsyncRun
    {
        [AsyncStateMachine(typeof(CatchPopSm))]
        public Task RunAsync()
        {
            var sm = new CatchPopSm();
            sm._builder = AsyncTaskMethodBuilder.Create();
            sm._state = -1;
            sm._builder.Start(ref sm);
            return sm._builder.Task;
        }

        internal struct CatchPopSm : IAsyncStateMachine
        {
            public int _state;
            public AsyncTaskMethodBuilder _builder;

            [SpanOnMethodProbeTestData]
            public void MoveNext()
            {
                var exception = new ExceptionReplayIntentionalException("catch pop first instruction");

                try
                {
                    throw exception;
                }
                catch
                {
                    _builder.SetException(exception);
                    return;
                }

#pragma warning disable CS0162
                _builder.SetResult();
#pragma warning restore CS0162
            }

            public void SetStateMachine(IAsyncStateMachine stateMachine)
            {
            }
        }
    }
}
#endif
