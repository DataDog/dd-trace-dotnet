// <copyright file="PubsubHeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    internal readonly struct PubsubHeadersCollectionAdapter : IHeadersCollection
    {
        private readonly IDictionary<string, string> _attributes;

        public PubsubHeadersCollectionAdapter(IDictionary<string, string> attributes)
        {
            _attributes = attributes;
        }

        public void Add(string name, string value)
        {
            _attributes[name] = value;
        }

        public IEnumerable<string> GetValues(string name)
        {
            return _attributes.TryGetValue(name, out var value)
                       ? new[] { value }
                       : Array.Empty<string>();
        }

        public void Remove(string name)
        {
            _attributes.Remove(name);
        }

        public void Set(string name, string value)
        {
            _attributes[name] = value;
        }
    }
}
