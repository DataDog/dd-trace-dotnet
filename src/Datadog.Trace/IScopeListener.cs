namespace Datadog.Trace
{
    internal interface IScopeListener
    {
        void AfterScopeActivated();

        void AfterScopeClosed();
    }
}
