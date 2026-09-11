#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    /// <summary>
    /// Finally is the last EH clause; the compiler-style <c>SetException</c> catch is not.
    /// <c>EndAsyncMethodProbe</c> used to inject at <c>GetEHPointer()[n-1]</c>, so the
    /// end-method probe landed in the finally. JIT accepted that IL, but capture failed
    /// with <c>EmptyCallStackTreeWhileCollecting</c>. Binding to the catch that contains
    /// <c>SetException</c> must restore Eligible capture.
    /// No probe or ExceptionReplay attributes — ProbesTests skips it, and it stays out of the snapshot suite.
    /// </summary>
    public class FinallyLastEhTest : IAsyncRun
    {
        // AsyncStateMachineAttribute is required so BeginMethod can resolve the kickoff
        // method. Without it GetAsyncKickoffMethod returns null, BeginMethod throws, and
        // capture stays EmptyCallStackTreeWhileCollecting even when EndMethod is in the catch.
        [AsyncStateMachine(typeof(FinallyLastSm))]
        public Task RunAsync()
        {
            var sm = new FinallyLastSm();
            sm._builder = AsyncTaskMethodBuilder.Create();
            sm._state = -1;
            sm._builder.Start(ref sm);
            return sm._builder.Task;
        }

        internal struct FinallyLastSm : IAsyncStateMachine
        {
            public int _state;
            public AsyncTaskMethodBuilder _builder;
            public bool _finallyRan;

            public void MoveNext()
            {
                try
                {
                    try
                    {
                        throw new ExceptionReplayIntentionalException("finally-last EH");
                    }
                    catch (Exception ex)
                    {
                        _builder.SetException(ex);
                        return;
                    }

#pragma warning disable CS0162
                    _builder.SetResult();
#pragma warning restore CS0162
                }
                finally
                {
                    _finallyRan = true;
                }
            }

            public void SetStateMachine(IAsyncStateMachine stateMachine)
            {
            }
        }
    }
}
#endif
