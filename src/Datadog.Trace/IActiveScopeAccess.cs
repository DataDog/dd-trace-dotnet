namespace Datadog.Trace
{
    internal interface IActiveScopeAccess
    {
        int Priority { get; }

        Scope GetActiveScope();

        bool TrySetActiveScope(Scope scope);
    }
}