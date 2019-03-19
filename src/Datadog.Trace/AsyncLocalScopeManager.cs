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

            if (current != scope)
            {
                Log.Warn("Specified scope is not the active scope.");
            }

            if (current == null)
            {
                Log.Error("Trying to close a null scope.");
                return;
            }

            _activeScope.Set(current.Parent);
        }
    }
}
