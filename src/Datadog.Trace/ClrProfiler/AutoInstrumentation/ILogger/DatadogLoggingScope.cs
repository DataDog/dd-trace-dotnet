// <copyright file="DatadogLoggingScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ILogger
{
    internal class DatadogLoggingScope : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly string _service;
        private readonly string _env;
        private readonly string _version;
        private readonly Tracer _tracer;

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
        }

        public int Count => 4;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return new KeyValuePair<string, object>("dd_service", _tracer.ActiveScope?.Span.ServiceName ?? _service);
                }
                else if (index == 1)
                {
                    return new KeyValuePair<string, object>("dd_env", _env);
                }
                else if (index == 2)
                {
                    return new KeyValuePair<string, object>("dd_version", _version);
                }
                else if (index == 3)
                {
                    return new KeyValuePair<string, object>("dd_trace_id", _tracer.ActiveScope?.Span.TraceId);
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public override string ToString()
        {
            var span = _tracer.ActiveScope?.Span;

            return string.Format(
                CultureInfo.InvariantCulture,
                "dd_service:{0}, dd_env:{1}, dd_version:{2}, dd_trace_id:{3}",
                span?.ServiceName ?? _service,
                _env,
                _version,
                _tracer.ActiveScope?.Span.TraceId);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var span = _tracer.ActiveScope?.Span;
            yield return new KeyValuePair<string, object>("dd_service", span?.ServiceName ?? _service);
            yield return new KeyValuePair<string, object>("dd_env", _env);
            yield return new KeyValuePair<string, object>("dd_version", _version);

            if (span is not null)
            {
                yield return new KeyValuePair<string, object>("dd_trace_id", span.TraceId);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
