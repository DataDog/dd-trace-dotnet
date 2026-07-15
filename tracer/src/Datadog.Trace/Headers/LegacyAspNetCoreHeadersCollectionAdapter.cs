// <copyright file="LegacyAspNetCoreHeadersCollectionAdapter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Headers
{
    /// <summary>
    /// Adapts an ASP.NET Core 2.x IHeaderDictionary without referencing Microsoft.Extensions.Primitives.StringValues.
    /// The duck-typing runtime boxes StringValues and converts it to the BCL IEnumerable implementation shared by
    /// ASP.NET Core 2.x versions.
    /// </summary>
    internal readonly struct LegacyAspNetCoreHeadersCollectionAdapter : IHeadersCollection
    {
        private readonly ILegacyAspNetCoreHeaders _headers;

        public LegacyAspNetCoreHeadersCollectionAdapter(ILegacyAspNetCoreHeaders headers)
        {
            _headers = headers;
        }

        public IEnumerable<string> GetValues(string name)
            => _headers[name] ?? [];

        public void Set(string name, string value) => throw new NotSupportedException();

        public void Add(string name, string value) => throw new NotSupportedException();

        public void Remove(string name) => throw new NotSupportedException();
    }
}

#endif
