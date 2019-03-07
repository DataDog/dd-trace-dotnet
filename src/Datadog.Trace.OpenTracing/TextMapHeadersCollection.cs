using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;
using OpenTracing.Propagation;

namespace Datadog.Trace.OpenTracing
{
    internal class TextMapHeadersCollection : IHeadersCollection
    {
        private readonly ITextMap _textMap;

        public TextMapHeadersCollection(ITextMap textMap)
        {
            _textMap = textMap;
        }

        public IEnumerable<string> GetValues(string name)
        {
            foreach (var pair in _textMap)
            {
                if (pair.Key == name)
                {
                    yield return pair.Value;
                }
            }
        }

        public void Set(string name, string value)
        {
            _textMap.Set(name, value);
        }

        public void Add(string name, string value)
        {
            throw new NotImplementedException();
        }

        public void Remove(string name)
        {
            throw new NotImplementedException();
        }
    }
}
