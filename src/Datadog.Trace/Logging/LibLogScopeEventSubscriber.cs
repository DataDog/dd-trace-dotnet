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

        // Each mapped context sets a key-value pair into the logging context
        // Disposing the context unsets the key-value pair
        //
        // IMPORTANT: The contexts must be closed in reverse-order of opening,
        //            so by convention always open the TraceId context before
        //            opening the SpanId context, and close the contexts in
        //            the opposite order
        private IDisposable _traceIdMappedContext;
        private IDisposable _spanIdMappedContext;

        public LibLogScopeEventSubscriber(IScopeManager scopeManager)
        {
            _scopeManager = scopeManager;
            _scopeManager.ScopeActivated += OnScopeActivated;
            _scopeManager.ScopeDeactivated += OnScopeDeactivated;

            SetDefaultValues();
        }

        public void OnScopeActivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            _spanIdMappedContext?.Dispose();
            _traceIdMappedContext?.Dispose();

            _activeScope = scopeEventArgs.Scope;
            _traceIdMappedContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, _activeScope.Span.TraceId, destructure: false);
            _spanIdMappedContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, _activeScope.Span.SpanId, destructure: false);
        }

        public void OnScopeDeactivated(object sender, ScopeEventArgs scopeEventArgs)
        {
            if (_activeScope != null && _activeScope.Equals(scopeEventArgs.Scope))
            {
                _spanIdMappedContext?.Dispose();
                _traceIdMappedContext?.Dispose();

                // Now we no longer have an active scope, reset the state to the default
                SetDefaultValues();
            }
        }

        public void Dispose()
        {
            _scopeManager.ScopeActivated -= OnScopeActivated;
            _scopeManager.ScopeDeactivated -= OnScopeDeactivated;

            _spanIdMappedContext?.Dispose();
            _traceIdMappedContext?.Dispose();
        }

        private void SetDefaultValues()
        {
            _activeScope = null;
            _traceIdMappedContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, 0, destructure: false);
            _spanIdMappedContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, 0, destructure: false);
        }
    }
}
