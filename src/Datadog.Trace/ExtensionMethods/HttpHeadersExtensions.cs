using System.Net.Http.Headers;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="HttpHeaders"/>.
    /// </summary>
    public static class HttpHeadersExtensions
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this HttpHeaders headers)
        {
            var propagator = new HttpHeadersPropagator(headers);
            return propagator.Extract();
        }

        /// <summary>
        /// Injects the specified <see cref="SpanContext"/> in the <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this HttpHeaders headers, SpanContext context)
        {
            var propagator = new HttpHeadersPropagator(headers);
            propagator.Inject(context);
        }
    }
}
