﻿// <copyright file="DatadogLoggingScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        private readonly string _cachedFormat;

        public DatadogLoggingScope()
            : this(Tracer.Instance)
        {
        }

        internal DatadogLoggingScope(Tracer tracer)
        {
            _tracer = tracer;
            _service = tracer.DefaultServiceName ?? string.Empty;
            _env = tracer.Settings.Environment ?? string.Empty;
            _version = tracer.Settings.ServiceVersion ?? string.Empty;
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
                    3 => new KeyValuePair<string, object>("dd_trace_id", _tracer.DistributedSpanContext?[HttpHeaderNames.TraceId] ?? "0"),
                    4 => new KeyValuePair<string, object>("dd_span_id", _tracer.DistributedSpanContext?[HttpHeaderNames.ParentId] ?? "0"),
                    _ => throw new ArgumentOutOfRangeException(nameof(index))
                };
            }
        }

        public override string ToString()
        {
            var spanContext = _tracer.DistributedSpanContext;
            if (spanContext is null
                || !spanContext.TryGetValue(HttpHeaderNames.TraceId, out string traceId)
                || !spanContext.TryGetValue(HttpHeaderNames.ParentId, out string spanId))
            {
                return _cachedFormat;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}, dd_trace_id:\"{1}\", dd_span_id:\"{2}\"",
                _cachedFormat,
                traceId,
                spanId);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var spanContext = _tracer.DistributedSpanContext;
            yield return new KeyValuePair<string, object>("dd_service", _service);
            yield return new KeyValuePair<string, object>("dd_env", _env);
            yield return new KeyValuePair<string, object>("dd_version", _version);

            if (spanContext is not null
                && spanContext.TryGetValue(HttpHeaderNames.TraceId, out string traceId)
                && spanContext.TryGetValue(HttpHeaderNames.ParentId, out string spanId))
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
