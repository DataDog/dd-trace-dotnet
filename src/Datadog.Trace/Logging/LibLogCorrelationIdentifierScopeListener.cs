using System;

namespace Datadog.Trace.Logging
{
    internal class LibLogCorrelationIdentifierScopeListener : IScopeListener, IDisposable
    {
        // Keep track of the active Scope (and its corresponding logging contexts)
        // so if another Scope is closed, no changes are made
        private Scope _activeScope;
        private IDisposable _activeTraceContext;
        private IDisposable _activeSpanContext;

        public void OnScopeActivated(Scope scope)
        {
            // Dispose of the previous contents since that scope is no longer active
            _activeTraceContext?.Dispose();
            _activeSpanContext?.Dispose();

            _activeScope = scope;
            _activeTraceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, CorrelationIdentifier.TraceId, destructure: false);
            _activeSpanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, CorrelationIdentifier.SpanId, destructure: false);
        }

        public void OnScopeClosed(Scope scope)
        {
            if (_activeScope.Equals(scope))
            {
                _activeTraceContext?.Dispose();
                _activeSpanContext?.Dispose();
            }
        }

        public void Dispose()
        {
            _activeTraceContext?.Dispose();
            _activeSpanContext?.Dispose();
        }
    }
}
