namespace Datadog.Trace
{
    internal class ScopeManager : IScopeManager
    {
        public Scope Active => DatadogScopeStack.Active;

        public Scope Activate(Span span, bool finishOnClose)
        {
            return new Scope(DatadogScopeStack.Active, span, finishOnClose);
        }
    }
}
