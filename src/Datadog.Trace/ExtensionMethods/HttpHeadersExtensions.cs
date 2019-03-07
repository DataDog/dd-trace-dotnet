using System;
using System.Net.Http.Headers;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="HttpHeaders"/> objects.
    /// </summary>
    internal static class HttpHeadersExtensions
    {
        /// <summary>
        /// Provides an <see cref="IHeadersCollection"/> implementation that wraps the specified <see cref="HttpHeaders"/>.
        /// </summary>
        /// <param name="headers">The HTTP headers to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static IHeadersCollection Wrap(this HttpHeaders headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            return new HttpHeadersCollection(headers);
        }
    }
}
