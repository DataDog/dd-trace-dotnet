using System;

namespace Datadog.Trace
{
    /// <summary>
    /// An API to access identifying values of the service and the active span
    /// </summary>
    public static class CorrelationIdentifier
    {
        internal static readonly string ServiceKey = "dd.service";
        internal static readonly string VersionKey = "dd.version";
        internal static readonly string EnvKey = "dd.env";
        internal static readonly string TraceIdKey = "dd.trace_id";
        internal static readonly string SpanIdKey = "dd.span_id";

        /// <summary>
        /// Gets the service of the application
        /// </summary>
        public static string Service
        {
            get
            {
                return Tracer.Instance.DefaultServiceName ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the version of the application
        /// </summary>
        public static string Version
        {
            get
            {
                return Tracer.Instance.Settings.ServiceVersion ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the env of the application
        /// </summary>
        public static string Env
        {
            get
            {
                return Tracer.Instance.Settings.Environment ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets the trace id of the active span
        /// </summary>
        public static ulong TraceId
        {
            get
            {
                return Tracer.Instance.ActiveScope?.Span?.TraceId ?? 0;
            }
        }

        /// <summary>
        /// Gets the span id of the active span
        /// </summary>
        public static ulong SpanId
        {
            get
            {
                return Tracer.Instance.ActiveScope?.Span?.SpanId ?? 0;
            }
        }
    }
}
