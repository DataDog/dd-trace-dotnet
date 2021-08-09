// <copyright file="KafkaHeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    internal struct KafkaHeadersCollectionAdapter : IHeadersCollection
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<KafkaHeadersCollectionAdapter>();
        private readonly IHeaders _headers;

        public KafkaHeadersCollectionAdapter(IHeaders headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // This only returns the _last_ bytes. Accessing other values is more expensive and should generally be unneccessary
            if (_headers.TryGetLastBytes(name, out var bytes))
            {
                try
                {
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }
                catch (Exception ex)
                {
                    Logger.Information(ex, "Could not deserialize Kafka header {headerName}", name);
                }
            }

            return Enumerable.Empty<string>();
        }

        public void Set(string name, string value)
        {
            Remove(name);
            Add(name, value);
        }

        public void Add(string name, string value)
        {
            _headers.Add(name, Encoding.UTF8.GetBytes(value));
        }

        public void Remove(string name)
        {
            _headers.Remove(name);
        }
    }
}
