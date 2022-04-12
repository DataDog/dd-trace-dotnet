// <copyright file="MetadataHeadersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc
{
    internal readonly struct MetadataHeadersCollection : IHeadersCollection
    {
        private readonly IMetadata _headers;

        public MetadataHeadersCollection(IMetadata? headers)
        {
            if (headers is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(headers));
            }

            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
        {
            return from object? header in _headers.GetAll(name)
                   where header is not null
                   select header.DuckCast<MetadataEntryStruct>() into entry
                   where !entry.IsBinary
                   select entry.Value;
        }

        public void Set(string name, string value)
        {
            Remove(name);
            _headers.Add(name, value);
        }

        public void Add(string name, string value)
        {
            _headers.Add(name, value);
        }

        public void Remove(string name)
        {
            var entry = _headers.Get(name);
            if (entry is not null)
            {
                _headers.Remove(name);
            }
        }
    }
}
