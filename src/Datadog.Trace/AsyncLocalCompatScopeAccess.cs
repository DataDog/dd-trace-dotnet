using System;

namespace Datadog.Trace
{
    internal class AsyncLocalCompatScopeAccess : IActiveScopeAccess
    {
        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public long CreatedAtTicks { get; } = DateTime.Now.Ticks;

        public Guid? ContextGuid { get; } = null;

        public int Priority => 0;

        public Scope GetActiveScope(params object[] parameters)
        {
            return _activeScope.Get();
        }

        public bool TrySetActiveScope(Scope scope, params object[] parameters)
        {
            _activeScope.Set(scope);
            return true;
        }
    }
}
