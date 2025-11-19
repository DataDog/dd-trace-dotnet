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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AzureMessagingCommon));

        /// <summary>
        /// Injects trace context into message properties dictionary
        /// </summary>
        public static void InjectContext(IDictionary<string, object>? properties, Scope? scope)
        {
            if (properties == null || scope?.Span?.Context == null)
            {
                return;
            }

            try
            {
                var context = new PropagationContext(scope.Span.Context, Baggage.Current);
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(
                    context,
                    properties,
                    default(DictionaryContextPropagation));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to inject trace context into message properties");
            }
        }

        /// <summary>
        /// Extracts full propagation context (span context and baggage) from message properties dictionary
        /// </summary>
        public static PropagationContext ExtractContext(IDictionary<string, object>? properties)
        {
            if (properties == null)
            {
                return default;
            }

            try
            {
                return Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                    properties,
                    default(DictionaryContextPropagation));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to extract propagation context from message properties");
                return default;
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
                    return [stringValue];
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
