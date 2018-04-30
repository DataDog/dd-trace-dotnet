using OpenTracing;

namespace Datadog.Trace.OpenTracing
{
    public class OpenTracingScopeManager : global::OpenTracing.IScopeManager
    {
        private readonly IScopeManager _scopeManager;
        private IScope _activeScope;

        public OpenTracingScopeManager(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;
        }

        IScope global::OpenTracing.IScopeManager.Active => _activeScope;

        IScope global::OpenTracing.IScopeManager.Activate(ISpan span, bool finishSpanOnDispose)
        {
            Span ddSpan = ((OpenTracingSpan)span).DatadogSpan;
            Scope ddScope = _scopeManager.Activate(ddSpan, finishSpanOnDispose);
            _activeScope = new OpenTracingScope(ddScope);
            return _activeScope;
        }
    }
}