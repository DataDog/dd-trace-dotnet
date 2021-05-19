using System;

namespace Datadog.Trace.Logging
{
    internal class NLogEnricher
    {
        private readonly object _versionProperty;
        private readonly object _environmentProperty;
        private readonly object _serviceProperty;
        private readonly object _traceIdProperty;
        private readonly object _spanIdProperty;

        public NLogEnricher(Tracer tracer)
        {
            _versionProperty = tracer.Settings.ServiceVersion;
            _environmentProperty = tracer.Settings.Environment;
            _serviceProperty = tracer.DefaultServiceName;
            _traceIdProperty = new TracerProperty(tracer, t => t.ActiveScope?.Span.TraceId.ToString());
            _spanIdProperty = new TracerProperty(tracer, t => t.ActiveScope?.Span.SpanId.ToString());
        }

        public IDisposable Register(ILogProvider logProvider)
        {
            return new Context(logProvider, this);
        }

        /// <summary>
        /// Wraps all the individual context objects in a single instance, that can be stored in an AsyncLocal
        /// </summary>
        private class Context : IDisposable
        {
            private readonly IDisposable _environment;
            private readonly IDisposable _version;
            private readonly IDisposable _service;
            private readonly IDisposable _traceId;
            private readonly IDisposable _spanId;

            public Context(ILogProvider logProvider, NLogEnricher enricher)
            {
                try
                {
                    _environment = logProvider.OpenMappedContext(CorrelationIdentifier.EnvKey, enricher._environmentProperty);
                    _version = logProvider.OpenMappedContext(CorrelationIdentifier.VersionKey, enricher._versionProperty);
                    _service = logProvider.OpenMappedContext(CorrelationIdentifier.ServiceKey, enricher._serviceProperty);
                    _traceId = logProvider.OpenMappedContext(CorrelationIdentifier.TraceIdKey, enricher._traceIdProperty);
                    _spanId = logProvider.OpenMappedContext(CorrelationIdentifier.SpanIdKey, enricher._spanIdProperty);
                }
                catch
                {
                    // Clear the properties that are already mapped
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                _environment?.Dispose();
                _version?.Dispose();
                _service?.Dispose();
                _traceId?.Dispose();
                _spanId?.Dispose();
            }
        }

        private class TracerProperty
        {
            private readonly Tracer _tracer;
            private readonly Func<Tracer, string> _getter;

            public TracerProperty(Tracer tracer, Func<Tracer, string> getter)
            {
                _tracer = tracer;
                _getter = getter;
            }

            public override string ToString()
            {
                return _getter(_tracer);
            }
        }
    }
}
