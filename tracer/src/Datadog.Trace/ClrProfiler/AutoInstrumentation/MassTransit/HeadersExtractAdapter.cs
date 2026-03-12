// <copyright file="HeadersExtractAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// IHeadersCollection adapter that reads from a MassTransit TransportHeaders object
    /// using reflection-based TryGetHeader calls. TryGetHeader is an explicit interface
    /// implementation on JsonTransportHeaders so duck typing cannot find it — reflection
    /// via TryGetHeaderValue works across all MT7 versions.
    /// </summary>
    internal readonly struct HeadersExtractAdapter : IHeadersCollection
    {
        private readonly object _headers;

        public HeadersExtractAdapter(object headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            var value = MassTransitCommon.TryGetHeaderValue(_headers, name);
            if (value != null)
            {
                return new[] { value };
            }

            return System.Array.Empty<string>();
        }

        public void Set(string name, string value)
        {
            // NOT USED — this adapter is read-only (extract only)
        }

        public void Add(string name, string value)
        {
            // NOT USED — this adapter is read-only (extract only)
        }

        public void Remove(string name)
        {
            // NOT USED — this adapter is read-only (extract only)
        }
    }
}
