// <copyright file="ContextPropagation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    internal readonly struct ContextPropagation : IHeadersCollection
    {
        private readonly IHeaders _headers;

        public ContextPropagation(IHeaders headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            if (_headers?.TryGetHeader(name, out var value) == true && value != null)
            {
                yield return value.ToString() ?? string.Empty;
            }
        }

        public void Set(string name, string value)
        {
            if (_headers != null)
            {
                _headers[name] = value;
            }
        }

        public void Add(string name, string value)
        {
            if (_headers != null)
            {
                _headers[name] = value;
            }
        }

        public void Remove(string name)
        {
            // MassTransit headers don't support removal
        }
    }
}
