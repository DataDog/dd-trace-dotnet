namespace Datadog.Trace
{
    internal interface IScopeManager
    {
        Scope Active { get; }

        Scope Activate(Span span, bool finishOnClose = true);

        void Close(Scope scope);
    }
}