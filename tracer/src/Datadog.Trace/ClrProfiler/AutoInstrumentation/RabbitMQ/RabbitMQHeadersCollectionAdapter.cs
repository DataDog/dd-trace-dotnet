// <copyright file="RabbitMQHeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal readonly struct RabbitMQHeadersCollectionAdapter : IBinaryHeadersCollection
    {
        private readonly IDictionary<string, object> _headers;

        public RabbitMQHeadersCollectionAdapter(IDictionary<string, object> headers)
        {
            _headers = headers;
        }

        public byte[] TryGetLastBytes(string name)
        {
            if (_headers.TryGetValue(name, out var value) && value is byte[] bytes)
            {
                return bytes;
            }

            return Array.Empty<byte>();
        }

        public void Add(string name, byte[] value)
        {
            _headers[name] = value;
        }
    }
}
