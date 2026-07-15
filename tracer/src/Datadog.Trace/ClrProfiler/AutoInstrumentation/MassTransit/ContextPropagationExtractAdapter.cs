// <copyright file="ContextPropagationExtractAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Adapter for EXTRACTING (reading) trace context headers from incoming MassTransit messages (consumer side).
    /// Scenario: When a consumer receives a message, extract distributed tracing headers to continue the trace.
    /// Uses duck typing to look up headers via IHeaders.TryGetHeader() — O(1) dictionary lookup.
    /// Used by: MassTransitCommon.ExtractTraceContext() for distributed tracing propagation.
    /// </summary>
    internal readonly struct ContextPropagationExtractAdapter : IHeadersCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContextPropagationExtractAdapter));

        private readonly IHeaders? _headersProxy;

        public ContextPropagationExtractAdapter(object? headers)
        {
            _headersProxy = headers?.DuckCast<IHeaders>();
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headersProxy == null)
            {
                return [];
            }

            try
            {
                if (_headersProxy.TryGetHeader(name, out var value) && value is string result)
                {
                    return [result];
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ContextPropagationExtractAdapter.GetValues: Error reading headers for '{Name}'", name);
            }

            return [];
        }

        public void Set(string name, string value)
        {
            // Not used - this adapter only extracts (reads) headers via GetValues(), never injects (writes) them.
            // Incoming message headers are read-only.
        }

        public void Add(string name, string value)
        {
            // Not used - incoming message headers are read-only.
        }

        public void Remove(string name)
        {
            // Not used - incoming message headers are read-only.
        }
    }
}
