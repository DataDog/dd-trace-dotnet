using System.Collections.Specialized;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="NameValueCollection"/>.
    /// </summary>
    public static class NameValueCollectionExtensions
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="headers">A collection of string names and value, such as HTTP headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this NameValueCollection headers)
        {
            var propagator = new NameValueCollectionPropagator(headers);
            return propagator.Extract();
        }

        /// <summary>
        /// Injects a <see cref="SpanContext"/> into the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="headers">A collection of string names and value, such as HTTP headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this NameValueCollection headers, SpanContext context)
        {
            var propagator = new NameValueCollectionPropagator(headers);
            propagator.Inject(context);
        }
    }
}
