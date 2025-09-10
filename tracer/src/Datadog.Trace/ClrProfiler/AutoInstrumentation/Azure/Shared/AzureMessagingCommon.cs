// <copyright file="AzureMessagingCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Shared
{
    internal static class AzureMessagingCommon
    {
        private const string LogPrefix = "[EventHubs] ";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureMessagingCommon));

        /// <summary>
        /// Injects trace context into message properties dictionary
        /// </summary>
        public static void InjectContext(IDictionary<string, object>? properties, Scope? scope)
        {
            if (properties == null || scope?.Span?.Context == null)
            {
                Log.Debug(
                    LogPrefix + "Skipping context injection: properties={0}, scope={1}",
                    properties == null,
                    scope?.Span?.Context == null);
                return;
            }

            try
            {
                var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(
                    context,
                    properties,
                    default(DictionaryContextPropagation));

                Log.Debug(
                    LogPrefix + "Successfully injected trace context: TraceId={0}, SpanId={1}",
                    scope.Span.TraceId,
                    scope.Span.SpanId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to inject trace context into message properties");
            }
        }

        /// <summary>
        /// Extracts trace context from message properties dictionary
        /// </summary>
        public static SpanContext? ExtractContext(IDictionary<string, object>? properties)
        {
            if (properties == null)
            {
                Log.Debug(LogPrefix + "Cannot extract context from null properties");
                return null;
            }

            try
            {
                var extractedContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                    properties,
                    default(DictionaryContextPropagation));

                if (extractedContext.SpanContext != null)
                {
                    Log.Debug(
                        LogPrefix + "Successfully extracted trace context: TraceId={0}, SpanId={1}",
                        extractedContext.SpanContext.TraceId128.ToString(),
                        extractedContext.SpanContext.SpanId);
                }
                else
                {
                    Log.Debug(LogPrefix + "No trace context found in message properties");
                }

                return extractedContext.SpanContext;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, LogPrefix + "Failed to extract trace context from message properties");
                return null;
            }
        }

        /// <summary>
        /// Context propagation helper for IDictionary string,object
        /// </summary>
        private readonly struct DictionaryContextPropagation : ICarrierGetter<IDictionary<string, object>>, ICarrierSetter<IDictionary<string, object>>
        {
            public IEnumerable<string> Get(IDictionary<string, object> carrier, string key)
            {
                if (carrier.TryGetValue(key, out var value) && value is string stringValue)
                {
                    return new[] { stringValue };
                }

                return System.Linq.Enumerable.Empty<string>();
            }

            public void Set(IDictionary<string, object> carrier, string key, string value)
            {
                carrier[key] = value;
            }
        }
    }
}
