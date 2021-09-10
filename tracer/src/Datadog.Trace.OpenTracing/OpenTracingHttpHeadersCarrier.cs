// <copyright file="OpenTracingHttpHeadersCarrier.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    /// <summary>
    /// This class wraps a <see cref="HttpHeaders"/> to implement the <see cref="ITextMap"/> interface
    /// to enable injecting context propagation headers on outgoing http requests.
    /// </summary>
    /// <seealso cref="ITextMap" />
    public class OpenTracingHttpHeadersCarrier : ITextMap
    {
        private HttpHeaders _headers;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenTracingHttpHeadersCarrier"/> class.
        /// </summary>
        /// <param name="headers">The <see cref="HttpHeaders"/> to wrap.</param>
        public OpenTracingHttpHeadersCarrier(HttpHeaders headers)
        {
            _headers = headers;
        }

        /// <summary>
        /// Returns the key's value from the underlying source, or null if the key doesn't exist.
        /// </summary>
        /// <param name="key">The key for which a value should be returned.</param>
        /// <returns>The key's value</returns>
        public string Get(string key)
        {
            _headers.TryGetValues(key, out IEnumerable<string> values);
            if (values == null)
            {
                return null;
            }

            // Comma appears to be the right separator to join several values for the same header name
            // source: https://stackoverflow.com/a/3097052
            return string.Join(",", values);
        }

        /// <summary>
        /// <para>Returns all key:value pairs from the underlying source.</para>
        /// <para>Note that for some Formats, the iterator may include entries that
        /// were never injected by a Tracer implementation (e.g., unrelated HTTP headers).</para>
        /// </summary>
        /// <returns>All key value pairs</returns>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in _headers)
            {
                yield return new KeyValuePair<string, string>(header.Key, header.Value == null ? null : string.Join(",", header.Value));
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the header collection.
        /// </summary>
        /// <returns>An System.Collections.IEnumerator object that can be used to iterate through the header collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Adds a key:value pair into the underlying source. If the source already contains the key, the value will be overwritten.
        /// </summary>
        /// <param name="key">A string, possibly with constraints dictated by the particular Format this <see cref="T:OpenTracing.Propagation.ITextMap" /> is paired with.</param>
        /// <param name="value">A String, possibly with constraints dictated by the particular Format this <see cref="T:OpenTracing.Propagation.ITextMap" /> is paired with.</param>
        public void Set(string key, string value)
        {
            // We remove all the existing values for that key before adding the new one to have a "set" behavior
            _headers.Remove(key);
            _headers.Add(key, value);
        }
    }
}
