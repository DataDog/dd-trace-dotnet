#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V5
{
    /// <summary>
    /// Duck-copy struct for RequestData
    /// </summary>
    public struct RequestDataV5 : IRequestData
    {
        private Proxy _data;

        public RequestDataV5(object source)
        {
            _data = source.DuckCast<Proxy>();
        }

        public string Path => _data.Path;

        public Uri Uri => _data.Uri;

        public string Method => _data.Method.ToString();

        [DuckCopy]
        public struct Proxy
        {
            public string Path;
            public HttpMethod Method;
            public Uri Uri;
        }
    }
}
