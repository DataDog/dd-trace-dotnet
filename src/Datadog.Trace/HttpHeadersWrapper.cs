using System.Net.Http.Headers;

namespace Datadog.Trace
{
    internal class HttpHeadersWrapper : IHeaderCollection
    {
        private readonly HttpHeaders _headers;

        public HttpHeadersWrapper(HttpHeaders headers)
        {
            _headers = headers;
        }

        public string Get(string name)
        {
            return _headers.TryGetValues(name, out var values)
                       ? string.Join(",", values)
                       : null;
        }

        public void Set(string name, string value)
        {
            _headers.Remove(name);
            _headers.Add(name, value);
        }
    }
}
