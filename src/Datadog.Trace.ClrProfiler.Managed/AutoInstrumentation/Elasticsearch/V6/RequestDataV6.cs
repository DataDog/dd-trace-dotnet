#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6
{
    /// <summary>
    /// Duck-copy struct for RequestData
    /// </summary>
    public struct RequestDataV6 : IRequestData
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
