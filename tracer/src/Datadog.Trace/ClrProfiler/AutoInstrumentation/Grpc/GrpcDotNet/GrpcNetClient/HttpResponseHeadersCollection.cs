// <copyright file="HttpResponseHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NET461

using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcNetClient
{
    internal readonly struct HttpResponseHeadersCollection : IHeadersCollection
    {
        private readonly HttpResponseHeaders _headers;

        public HttpResponseHeadersCollection(HttpResponseHeaders headers)
        {
            if (headers is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(headers));
            }

            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers.TryGetValues(name, out IEnumerable<string> values))
            {
                return values;
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, value);
        }

        public void Add(string name, string value) => _headers.Add(name, value);

        public void Remove(string name) => _headers.Remove(name);
    }
}
#endif
