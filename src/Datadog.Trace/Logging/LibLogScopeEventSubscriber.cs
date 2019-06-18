using System;

namespace Datadog.Trace.Logging
{
    internal class LibLogScopeEventSubscriber : IDisposable
    {
        // Keep track of the active Scope (and its corresponding logging contexts)
        // so if another Scope is deactivated (not the active Scope), no
        // changes are made
        private IScopeManager _scopeManager;
        private Scope _activeScope;
        private IDisposable _activeTraceContext;
        private IDisposable _activeSpanContext;

        public LibLogScopeEventSubscriber(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;
            _scopeManager.ScopeActivated += OnScopeActivated;
            _scopeManager.ScopeDeactivated += OnScopeDeactivated;

            _activeTraceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, 0, destructure: false);
            _activeSpanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, 0, destructure: false);
        }

        public void OnScopeActivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            // Contexts MUST be closed in reverse order of opening
            _activeSpanContext?.Dispose();
            _activeTraceContext?.Dispose();

            _activeScope = scopeEventArgs.Scope;
            _activeTraceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, _activeScope.Span.TraceId, destructure: false);
            _activeSpanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, _activeScope.Span.SpanId, destructure: false);
        }

        public void OnScopeDeactivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            if (_activeScope != null && _activeScope.Equals(scopeEventArgs.Scope))
            {
                // Contexts MUST be closed in reverse order of opening
                _activeSpanContext?.Dispose();
                _activeTraceContext?.Dispose();

                // Now we no longer have an active scope, so reset to zero values
                _activeScope = null;
                _activeTraceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, 0, destructure: false);
                _activeSpanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, 0, destructure: false);
            }
        }

        public void Dispose()
        {
            _scopeManager.ScopeActivated -= OnScopeActivated;
            _scopeManager.ScopeDeactivated -= OnScopeDeactivated;

            // Contexts MUST be closed in reverse order of opening
            _activeSpanContext?.Dispose();
            _activeTraceContext?.Dispose();
        }
    }
}
