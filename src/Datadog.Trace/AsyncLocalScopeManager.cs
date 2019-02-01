using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager
    {
        private static readonly ILog _log = LogProvider.For<AsyncLocalScopeManager>();

        private readonly AsyncLocalCompat<Scope> _currentSpan = new AsyncLocalCompat<Scope>();

        public Scope Active => _currentSpan.Get();

        public Scope Activate(Span span, bool finishOnClose = true)
        {
            var current = _currentSpan.Get();
            var scope = new Scope(current, span, this, finishOnClose);
            _currentSpan.Set(scope);
            return scope;
        }

        public void Close(Scope scope)
        {
            var current = _currentSpan.Get();

            if (current == null)
            {
                _log.Error("Trying to close a null scope.");
                return;
            }

            if (current != scope)
            {
                _log.Warn("Specified scope is not the active scope.");
            }

            _currentSpan.Set(current.Parent);
        }
    }
}
