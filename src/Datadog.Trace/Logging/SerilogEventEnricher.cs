#pragma warning disable SA1201 // Elements should appear in the correct order

using System;
using System.Linq.Expressions;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    internal class SerilogEventEnricher
    {
        private readonly Tracer _tracer;

        private readonly object _serviceProperty;
        private readonly object _versionProperty;
        private readonly object _environmentProperty;
        private readonly Func<object, object> _valueFactory;
        private readonly Func<string, object, object> _propertyFactory;

        public SerilogEventEnricher(Tracer tracer)
        {
            var logEventPropertyType = Type.GetType("Serilog.Events.LogEventProperty, Serilog");

            if (logEventPropertyType == null)
            {
                throw new LibLogException("Serilog.Events.LogEventProperty not found");
            }

            var logEventPropertyValueType = Type.GetType("Serilog.Events.LogEventPropertyValue, Serilog");

            if (logEventPropertyValueType == null)
            {
                throw new LibLogException("Serilog.Events.LogEventPropertyValue not found");
            }

            var scalarValueType = Type.GetType("Serilog.Events.ScalarValue, Serilog");

            if (scalarValueType == null)
            {
                throw new LibLogException("Serilog.Events.ScalarValue not found");
            }

            _tracer = tracer;

            _valueFactory = BuildValueFactory(scalarValueType);
            _propertyFactory = BuildPropertyFactory(logEventPropertyType, logEventPropertyValueType);

            _serviceProperty = _propertyFactory(CorrelationIdentifier.SerilogServiceKey, _valueFactory(tracer.DefaultServiceName));
            _versionProperty = _propertyFactory(CorrelationIdentifier.SerilogVersionKey, _valueFactory(tracer.Settings.ServiceVersion));
            _environmentProperty = _propertyFactory(CorrelationIdentifier.SerilogEnvKey, _valueFactory(tracer.Settings.Environment));
        }

        [DuckReverseMethod("Serilog.Events.LogEvent", "Serilog.Core.ILogEventPropertyFactory")]
        public void Enrich(ILogEvent logEvent, object propertyFactory)
        {
            var activeScope = _tracer.ActiveScope;

            if (activeScope == null)
            {
                return;
            }

            var traceIdProperty = _propertyFactory(CorrelationIdentifier.SerilogTraceIdKey, _valueFactory(activeScope.Span.TraceId.ToString()));
            var spanIdProperty = _propertyFactory(CorrelationIdentifier.SerilogSpanIdKey, _valueFactory(activeScope.Span.SpanId.ToString()));

            logEvent.AddPropertyIfAbsent(_serviceProperty);
            logEvent.AddPropertyIfAbsent(_versionProperty);
            logEvent.AddPropertyIfAbsent(_environmentProperty);
            logEvent.AddPropertyIfAbsent(traceIdProperty);
            logEvent.AddPropertyIfAbsent(spanIdProperty);
        }

        private static Func<string, object, object> BuildPropertyFactory(Type logEventPropertyType, Type logEventPropertyValueType)
        {
            // Create an expression to call `new LogEventProperty(string name, LogEventPropertyValuevalue)`
            var nameParam = Expression.Parameter(typeof(string), "name");
            var propertyValueParam = Expression.Parameter(typeof(object), "value");

            var constructor = logEventPropertyType.GetConstructor(new[] { typeof(string), logEventPropertyValueType });

            var castPropertyValueParam = Expression.Convert(propertyValueParam, logEventPropertyValueType);
            var newExpression = Expression.New(constructor, nameParam, castPropertyValueParam);

            return Expression.Lambda<Func<string, object, object>>(
                    newExpression,
                    nameParam,
                    propertyValueParam)
                .Compile();
        }

        private static Func<object, object> BuildValueFactory(Type scalarValueType)
        {
            // Create an expression to call `new ScalarValue(object value)`
            var valueParam = Expression.Parameter(typeof(object), "value");

            var constructor = scalarValueType.GetConstructor(new[] { typeof(object) });

            var newExpression = Expression.New(constructor, valueParam);

            return Expression.Lambda<Func<object, object>>(
                    newExpression,
                    valueParam)
                .Compile();
        }

        public interface ILogEvent
        {
            void AddPropertyIfAbsent(object property);
        }
    }
}
