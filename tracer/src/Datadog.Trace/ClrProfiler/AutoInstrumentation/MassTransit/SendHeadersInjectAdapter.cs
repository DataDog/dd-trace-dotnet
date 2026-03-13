// <copyright file="SendHeadersInjectAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// IHeadersCollection adapter that writes to an ISendHeaders duck type proxy using Set().
    /// Used to inject trace context into MassTransit SendContext headers via duck typing,
    /// which calls the SendHeaders.Set() interface method available in all MT7 versions.
    /// </summary>
    internal readonly struct SendHeadersInjectAdapter : IHeadersCollection
    {
        private readonly ISendHeaders _headers;

        public SendHeadersInjectAdapter(ISendHeaders headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            // NOT USED — this adapter is write-only (inject only)
            return System.Array.Empty<string>();
        }

        public void Set(string name, string value)
        {
            _headers.Set(name, value);
        }

        public void Add(string name, string value)
        {
            _headers.Set(name, value);
        }

        public void Remove(string name)
        {
            // NOT USED — header removal is not needed for trace context injection
        }
    }
}
