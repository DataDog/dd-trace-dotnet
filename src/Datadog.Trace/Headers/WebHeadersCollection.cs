#if !NETSTANDARD2_0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Datadog.Trace.Headers
{
    /// <summary>
    /// A <see cref="WebHeaderCollection"/> that supports our <see cref="IHeadersCollection"/> interface.>
    /// </summary>
    public class WebHeadersCollection : IHeadersCollection
    {
        private readonly WebHeaderCollection _headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebHeadersCollection"/> class.
        /// </summary>
        /// <param name="headers">A <see cref="WebHeaderCollection"/>.</param>
        public WebHeadersCollection(WebHeaderCollection headers)
        {
            _headers = headers ?? throw new ArgumentNullException(nameof(headers));
        }

        /// <summary>
        /// Returns an IEnumerable/>
        /// </summary>
        /// <param name="name">Collection variable name.</param>
        /// <returns>IEnumerable</returns>
        public IEnumerable<string> GetValues(string name)
            => _headers.GetValues(name) ?? Enumerable.Empty<string>();

        /// <summary>
        /// Sets name and value for existing item.
        /// </summary>
        /// <param name="name">Key</param>
        /// <param name="value">Value</param>
        public void Set(string name, string value)
            => _headers.Set(name, value);

        /// <summary>
        /// Adds a new item.
        /// </summary>
        /// <param name="name">Key</param>
        /// <param name="value">Value</param>
        public void Add(string name, string value)
            => _headers.Add(name, value);

        /// <summary>
        /// Removes an item by key name.
        /// </summary>
        /// <param name="name">Key</param>
        public void Remove(string name)
            => _headers.Remove(name);
    }
}

#endif
