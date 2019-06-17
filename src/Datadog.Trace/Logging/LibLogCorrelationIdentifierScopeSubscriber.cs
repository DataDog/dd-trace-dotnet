using System;

namespace Datadog.Trace.Logging
{
    internal class LibLogCorrelationIdentifierScopeSubscriber : IDisposable
    {
        // Keep track of the active Scope (and its corresponding logging contexts)
        // so if another Scope is deactivated (not the active Scope), no
        // changes are made
        private IScopeManager _scopeManager;
        private Scope _activeScope;
        private IDisposable _activeTraceContext;
        private IDisposable _activeSpanContext;

        public LibLogCorrelationIdentifierScopeSubscriber(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;
            _scopeManager.ScopeActivated += OnScopeActivated;
            _scopeManager.ScopeDeactivated += OnScopeDeactivated;
        }

        public void OnScopeActivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            // Dispose of the previous contents since that scope is no longer active
            _activeTraceContext?.Dispose();
            _activeSpanContext?.Dispose();

            _activeScope = scopeEventArgs.Scope;
            _activeTraceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, _activeScope.Span.TraceId, destructure: false);
            _activeSpanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, _activeScope.Span.SpanId, destructure: false);
        }

        public void OnScopeDeactivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            if (_activeScope.Equals(scopeEventArgs.Scope))
            {
                _activeTraceContext?.Dispose();
                _activeSpanContext?.Dispose();
            }
        }

        public void Dispose()
        {
            _scopeManager.ScopeActivated -= OnScopeActivated;
            _scopeManager.ScopeDeactivated -= OnScopeDeactivated;

            _activeTraceContext?.Dispose();
            _activeSpanContext?.Dispose();
        }
    }
}
