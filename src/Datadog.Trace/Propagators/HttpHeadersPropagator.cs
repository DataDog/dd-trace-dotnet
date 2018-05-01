using System.Net.Http.Headers;

namespace Datadog.Trace.Propagators
{
    /// <summary>
    /// A HTTP Propagator using <see cref="HttpHeaders"/> as carrier
    /// </summary>
    public static class HttpHeadersPropagator
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this HttpHeaders headers)
        {
            var wrapper = new HttpHeadersWrapper(headers);
            return wrapper.Extract();
        }

        /// <summary>
        /// Injects the specified <see cref="SpanContext"/> in the <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this HttpHeaders headers, SpanContext context)
        {
            var wrapper = new HttpHeadersWrapper(headers);
            wrapper.Inject(context);
        }
    }
}
