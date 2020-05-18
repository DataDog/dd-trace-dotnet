#if !NETSTANDARD2_0

using System;
using System.Net;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for <see cref="WebHeaderCollection"/> objects.
    /// </summary>
    public static class WebHeadersExtensions
    {
        /// <summary>
        /// Provides an <see cref="IHeadersCollection"/> implementation that wraps the specified <see cref="WebHeaderCollection"/>.
        /// </summary>
        /// <param name="headers">The Web headers to wrap.</param>
        /// <returns>An object that implements <see cref="IHeadersCollection"/>.</returns>
        public static IHeadersCollection Wrap(this WebHeaderCollection headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            return new WebHeadersCollection(headers);
        }
    }
}

#endif
