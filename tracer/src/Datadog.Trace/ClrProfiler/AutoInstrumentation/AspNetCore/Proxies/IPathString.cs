// <copyright file="IPathString.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.Proxies
{
    /// <summary>
    /// Microsoft.AspNetCore.Http.PathString interface for ducktyping
    /// </summary>
    internal interface IPathString : IDuckType
    {
        bool HasValue { get; }

        [Duck(ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.PathString, Microsoft.AspNetCore.Http.Abstractions" })]
        object Add(object otherPathString);

        [Duck(ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.PathString, Microsoft.AspNetCore.Http.Abstractions", "System.StringComparison" })]
        bool Equals(object other, StringComparison comparisonType);

        [Duck(ParameterTypeNames = new[] { "Microsoft.AspNetCore.Http.PathString, Microsoft.AspNetCore.Http.Abstractions", "System.StringComparison", "Microsoft.AspNetCore.Http.PathString, Microsoft.AspNetCore.Http.Abstractions" })]
        bool StartsWithSegments(object other, StringComparison comparisonType, out object remaining);

        string ToUriComponent();
    }
}
#endif
