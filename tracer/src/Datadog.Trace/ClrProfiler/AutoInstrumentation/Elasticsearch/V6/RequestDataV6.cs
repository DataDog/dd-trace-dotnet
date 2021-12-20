// <copyright file="RequestDataV6.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6
{
    /// <summary>
    /// Duck-copy struct for RequestData
    /// </summary>
    internal struct RequestDataV6 : IRequestData
    {
        private Proxy _data;

        public RequestDataV6(object source)
        {
            _data = source.DuckCast<Proxy>();
        }

        public string Path => _data.PathAndQuery;

        public Uri Uri => _data.Uri;

        public string Method => _data.Method.ToString();

        [DuckCopy]
        public struct Proxy
        {
            public string PathAndQuery;
            public HttpMethod Method;
            public Uri Uri;
        }
    }
}
