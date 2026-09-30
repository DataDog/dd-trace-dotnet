#if NETCOREAPP3_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.ExceptionReplay
{
    /// <summary>
    /// Adversarial MoveNext: store the catch exception in a field, then
    /// <c>SetException(_captured)</c>. Roslyn emits <c>ldfld</c> immediately before
    /// <c>SetException</c>. Stock compiler async uses <c>ldloc</c> instead; this fixture
    /// exists to prove EndAsyncMethodProbe's memcpy of <c>m_pPrev</c> is JIT-invalid
    /// when that opcode is not a standalone value load.
    /// </summary>
    public class SetExceptionLdfldPrevTest : IAsyncRun
    {
        public Task RunAsync()
        {
            var sm = new LdfldSm();
            sm._builder = AsyncTaskMethodBuilder.Create();
            sm._state = -1;
            sm._builder.Start(ref sm);
            return sm._builder.Task;
        }

        internal struct LdfldSm : IAsyncStateMachine
        {
            public int _state;
            public AsyncTaskMethodBuilder _builder;
            public Exception _captured;

            public void MoveNext()
            {
                try
                {
                    throw new InvalidOperationException("APMS-20228 setexception ldfld prev");
                }
                catch (Exception ex)
                {
                    _captured = ex;
                    _builder.SetException(_captured);
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
