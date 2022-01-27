// <copyright file="IHeaderDictionary.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    internal interface IHeaderDictionary
    {
        [Duck(ExplicitInterfaceTypeName = "System.Collections.Generic.IDictionary<System.String,Microsoft.Extensions.Primitives.StringValues>")]
        bool TryGetValue(string key, out IEnumerable<string> value);

        [Duck(ExplicitInterfaceTypeName = "System.Collections.Generic.IDictionary<System.String,Microsoft.Extensions.Primitives.StringValues>")]
        bool Remove(string key);
    }
}
#endif
