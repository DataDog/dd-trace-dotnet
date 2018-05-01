using System;

namespace Datadog.Trace.Propagators
{
    /// <summary>
    /// A HTTP Propagator using <see cref="IHeaderCollection"/> as carrier
    /// </summary>
    public static class HeaderCollectionPropagator
    {
        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the specified headers.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>The <see cref="SpanContext"/></returns>
        public static SpanContext Extract(this IHeaderCollection headers)
        {
            var parentIdHeader = headers.Get(Constants.HttpHeaderParentId);
            if (parentIdHeader == null)
            {
                return null;
            }

            var traceIdHeader = headers.Get(Constants.HttpHeaderTraceId);
            if (traceIdHeader == null)
            {
                return null;
            }

            ulong parentId;
            try
            {
                parentId = Convert.ToUInt64(parentIdHeader);
            }
            catch (FormatException)
            {
                return null;
            }

            ulong traceId;
            try
            {
                traceId = Convert.ToUInt64(traceIdHeader);
            }
            catch (FormatException)
            {
                return null;
            }

            return new SpanContext(traceId, parentId);
        }

        /// <summary>
        /// Injects the specified <see cref="SpanContext"/> in the <see cref="IHeaderCollection"/>.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="context">The context.</param>
        public static void Inject(this IHeaderCollection headers, SpanContext context)
        {
            headers.Set(Constants.HttpHeaderParentId, context.SpanId.ToString());
            headers.Set(Constants.HttpHeaderTraceId, context.TraceId.ToString());
        }
    }
}
