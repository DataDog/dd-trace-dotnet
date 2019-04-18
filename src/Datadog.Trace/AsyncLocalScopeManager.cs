using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : IScopeManager
    {
        private static readonly ILog Log = LogProvider.For<AsyncLocalScopeManager>();

        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public Scope Active => _activeScope.Get();

        public Scope Activate(Span span, bool finishOnClose)
        {
            var activeScope = _activeScope.Get();
            var scope = new Scope(activeScope, span, this, finishOnClose);
            _activeScope.Set(scope);
            return scope;
        }

        public void Close(Scope scope)
        {
            var current = _activeScope.Get();

            if (current != null && current == scope)
            {
                // if the scope that was just closed was the active scope,
                // set its parent as the new active scope
                _activeScope.Set(current.Parent);
            }
        }
    }
}
