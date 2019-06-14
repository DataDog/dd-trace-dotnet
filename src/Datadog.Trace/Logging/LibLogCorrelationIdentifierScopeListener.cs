using System;

namespace Datadog.Trace.Logging
{
    internal class LibLogCorrelationIdentifierScopeListener : IScopeListener, IDisposable
    {
        private IDisposable _traceContext;
        private IDisposable _spanContext;

        public void AfterScopeActivated()
        {
            _traceContext = LogProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, CorrelationIdentifier.TraceId, destructure: false);
            _spanContext = LogProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, CorrelationIdentifier.SpanId, destructure: false);
        }

        public void AfterScopeClosed()
        {
            _traceContext?.Dispose();
            _spanContext?.Dispose();
        }

        public void Dispose()
        {
            _traceContext?.Dispose();
            _spanContext?.Dispose();
        }
    }
}
