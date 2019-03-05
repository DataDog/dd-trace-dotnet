using System;
using System.Collections.Specialized;
using System.Net.Http.Headers;

namespace Datadog.Trace.Headers
{
    /// <summary>
    /// Provides static methods to wrap different header implementations in a <see cref="IHeadersCollection"/>.
    /// </summary>
    public static class HeadersFactory
    {
        /// <summary>
        /// Provides an <see cref="IHeadersCollection"/> implementation that wraps the specified <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The HTTP headers to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static IHeadersCollection Wrap(HttpHeaders headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            return new HttpHeadersCollection(headers);
        }

        /// <summary>
        /// Provides an <see cref="IHeadersCollection"/> implementation that wraps the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="collection">The name/value collection to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static IHeadersCollection Wrap(NameValueCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            return new NameValueHeadersCollection(collection);
        }
    }
}
