namespace Datadog.Trace
{
    internal interface IScopeListener
    {
        void OnScopeActivated(Scope scope);

        void OnScopeClosed(Scope scope);
    }
}
