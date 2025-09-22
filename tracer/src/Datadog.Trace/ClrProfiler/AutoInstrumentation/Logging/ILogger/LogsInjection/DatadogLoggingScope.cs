// <copyright file="DatadogLoggingScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger
{
    internal class DatadogLoggingScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly string _service;
        private readonly string _env;
        private readonly string _version;
        private readonly Tracer _tracer;
        private readonly bool _use128Bits;
        private readonly string _cachedFormat;

        public DatadogLoggingScope()
            : this(Tracer.Instance)
        {
        }

        internal DatadogLoggingScope(Tracer tracer)
        {
            _tracer = tracer;
            // TODO: Subscribe to changes in settings
            var mutableSettings = tracer.CurrentTraceSettings.Settings;
            _service = mutableSettings.DefaultServiceName;
            _env = mutableSettings.Environment ?? string.Empty;
            _version = mutableSettings.ServiceVersion ?? string.Empty;
            _use128Bits = _tracer.Settings.TraceId128BitLoggingEnabled;

            _cachedFormat = string.Format(
                CultureInfo.InvariantCulture,
                "dd_service:\"{0}\", dd_env:\"{1}\", dd_version:\"{2}\"",
                _service,
                _env,
                _version);
        }

        public int Count => 5;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                return index switch
                {
                    0 => new KeyValuePair<string, object>("dd_service", _service),
                    1 => new KeyValuePair<string, object>("dd_env", _env),
                    2 => new KeyValuePair<string, object>("dd_version", _version),

                    // We want to get trace id and span id separately here,
                    // hence the separate TryGetTraceId() and TryGetSpanId() methods in LogContext.
                    3 => new KeyValuePair<string, object>(
                        "dd_trace_id",
                        _tracer.DistributedSpanContext is { } context && LogContext.TryGetTraceId(context, _use128Bits, out var traceId) ? traceId : "0"),

                    4 => new KeyValuePair<string, object>(
                        "dd_span_id",
                        _tracer.DistributedSpanContext is { } context && LogContext.TryGetSpanId(context, out var spanId) ? spanId : "0"),

                    _ => throw new ArgumentOutOfRangeException(nameof(index))
                };
            }
        }

        public override string ToString()
        {
            if (_tracer.DistributedSpanContext is { } context &&
                LogContext.TryGetValues(context, out var traceId, out var spanId, _use128Bits))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, dd_trace_id:\"{1}\", dd_span_id:\"{2}\"",
                    _cachedFormat,
                    traceId,
                    spanId);
            }

            return _cachedFormat;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>("dd_service", _service);
            yield return new KeyValuePair<string, object>("dd_env", _env);
            yield return new KeyValuePair<string, object>("dd_version", _version);

            if (_tracer.DistributedSpanContext is { } context &&
                LogContext.TryGetValues(context, out var traceId, out var spanId, _use128Bits))
            {
                yield return new KeyValuePair<string, object>("dd_trace_id", traceId);
                yield return new KeyValuePair<string, object>("dd_span_id", spanId);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
