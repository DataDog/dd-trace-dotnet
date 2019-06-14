namespace Datadog.Trace
{
    internal interface IScopeListener
    {
        void AfterScopeActivated(Scope scope);

        void AfterScopeClosed(Scope scope);
    }
}
