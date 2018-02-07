using Microsoft.AspNetCore.Http;

namespace Datadog.Trace.AspNetCore
{
    internal class HeaderDictionaryWrapper : IHeaderCollection
    {
        private readonly IHeaderDictionary _headers;

        public HeaderDictionaryWrapper(IHeaderDictionary headers)
        {
            _headers = headers;
        }

        public string Get(string name)
        {
            _headers.TryGetValue(name, out var value);
            return value;
        }

        public void Set(string name, string value)
        {
            _headers[name] = value;
        }
    }
}
