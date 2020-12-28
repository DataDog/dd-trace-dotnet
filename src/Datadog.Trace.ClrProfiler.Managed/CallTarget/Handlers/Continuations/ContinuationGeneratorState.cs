namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal readonly struct ContinuationGeneratorState<TTarget>
    {
        public readonly TTarget Target;
        public readonly CallTargetState State;

        public ContinuationGeneratorState(TTarget target, CallTargetState state)
        {
            Target = target;
            State = state;
        }
    }
}
