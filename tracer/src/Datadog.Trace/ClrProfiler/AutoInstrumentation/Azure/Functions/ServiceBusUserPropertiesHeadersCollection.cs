// <copyright file="ServiceBusUserPropertiesHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Helper class to adapt ServiceBus UserProperties dictionary for context extraction.
    /// Implements IHeadersCollection to work with SpanContextPropagator.Extract.
    /// </summary>
    internal sealed class ServiceBusUserPropertiesHeadersCollection : IHeadersCollection
    {
        private readonly IDictionary<string, object> _userProperties;

        public ServiceBusUserPropertiesHeadersCollection(IDictionary<string, object> userProperties)
        {
            _userProperties = userProperties;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_userProperties.TryGetValue(name, out var value) && value?.ToString() is { } stringValue)
            {
                return new[] { stringValue };
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _userProperties[name] = value;
        }

        public void Add(string name, string value)
        {
            if (_userProperties.ContainsKey(name))
            {
                // If key exists, we could append or replace. For simplicity, replace.
                _userProperties[name] = value;
            }
            else
            {
                _userProperties[name] = value;
            }
        }

        public void Remove(string name)
        {
            _userProperties.Remove(name);
        }
    }
}
#endif
