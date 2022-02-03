// <copyright file="IRequestCookieCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore
{
    internal interface IRequestCookieCollection : IEnumerable<KeyValuePair<string, string>>
    {
        int Count { get; }

        ICollection<string> Keys { get; }
    }
}
#endif
