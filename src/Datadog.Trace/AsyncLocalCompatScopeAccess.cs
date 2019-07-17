namespace Datadog.Trace
{
    internal class AsyncLocalCompatScopeAccess : IActiveScopeAccess
    {
        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public int Priority => 0;

        public Scope GetActiveScope()
        {
            return _activeScope.Get();
        }

        public bool TrySetActiveScope(Scope scope)
        {
            _activeScope.Set(scope);
            return true;
        }
    }
}