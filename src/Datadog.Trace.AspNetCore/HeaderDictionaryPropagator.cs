using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AspNetCore
{
    /// <summary>
    /// A HTTP Propagator using <see cref="IHeaderDictionary"/> as carrier
    /// </summary>
    public static class HeaderDictionaryPropagator
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified <see cref="IHeaderDictionary"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this IHeaderDictionary headers)
        {
            var wrapper = new HeaderDictionaryWrapper(headers);
            return wrapper.Extract();
        }

        /// <summary>
        /// Injects the specified <see cref="SpanContext"/> in the <see cref="IHeaderDictionary"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this IHeaderDictionary headers, SpanContext context)
        {
            var wrapper = new HeaderDictionaryWrapper(headers);
            wrapper.Inject(context);
        }
    }
}
