namespace Datadog.Trace
{
    internal interface IAmbientContextAccess
    {
        int Priority { get; }

        Scope GetActiveScope();

        bool TrySetActiveScope(Scope scope);
    }
}
