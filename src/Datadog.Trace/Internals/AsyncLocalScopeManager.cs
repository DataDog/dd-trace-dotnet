using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager
    {
        private static ILog _log = LogProvider.For<AsyncLocalScopeManager>();

        private AsyncLocalCompat<SpanBase> _currentSpan = new AsyncLocalCompat<SpanBase>("Datadog.Trace.AsyncLocalScopeManager._currentSpan");

        public SpanBase Active => _currentSpan.Get();

        public void Activate(SpanBase span)
        {
            var current = _currentSpan.Get();
            _currentSpan.Set(span);

            // TODO make span aware of its predecessor
        }

        public void Desactivate(SpanBase span)
        {
            var current = _currentSpan.Get();
            if (current != span)
            {
                _log.Warn("Current span doesn't match desactivated span");
            }

            // TODO activate parent of current span
        }
    }
}
