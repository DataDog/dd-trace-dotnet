// <copyright file="HttpHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient
{
    internal readonly struct HttpHeadersCollection : IHeadersCollection
    {
        private readonly IRequestHeaders _headers;

        public HttpHeadersCollection(IRequestHeaders headers)
        {
            if (headers is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(headers));
            }

            _headers = headers;
        }

        public StringEnumerable GetValues(string name) => _headers.TryGetValues(name, out var values) ?
                                                              new StringEnumerable(values) :
                                                              StringEnumerable.Empty;

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, value);
        }
    }
}
