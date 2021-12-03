// <copyright file="LogEnricher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Logging
{
    /// <summary>
    /// Represents the context needed for log injection for a given tracer
    /// </summary>
    internal class LogEnricher : ILogEnricher
    {
        private readonly ILogProvider _logProvider;

        private object _versionProperty;
        private object _environmentProperty;
        private object _serviceProperty;
        private object _traceIdProperty;
        private object _spanIdProperty;

        public LogEnricher(ILogProvider logProvider)
        {
            _logProvider = logProvider;
        }

        public void Initialize(string defaultServiceName, string version, string env)
        {
            _versionProperty = version;
            _environmentProperty = env;
            _serviceProperty = defaultServiceName;
            _traceIdProperty = CreateTracerProperty(() => Tracer.Instance.DistributedSpanContext?[HttpHeaderNames.TraceId]);
            _spanIdProperty = CreateTracerProperty(() => Tracer.Instance.DistributedSpanContext?[HttpHeaderNames.ParentId]);
        }

        public IDisposable Register()
        {
            return new Context(_logProvider, this);
        }

        protected virtual object CreateTracerProperty(Func<string> getter) => new TracerProperty(getter);

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

            public Context(ILogProvider logProvider, LogEnricher enricher)
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
            private readonly Func<string> _getter;

            public TracerProperty(Func<string> getter)
            {
                _getter = getter;
            }

            public override string ToString()
            {
                return _getter();
            }
        }
    }
}
