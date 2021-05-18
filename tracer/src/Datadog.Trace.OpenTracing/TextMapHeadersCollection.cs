// <copyright file="TextMapHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
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
