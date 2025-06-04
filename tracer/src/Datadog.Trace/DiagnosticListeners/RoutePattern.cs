// <copyright file="RoutePattern.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// RoutePattern for duck typing
    /// </summary>
    [DuckCopy]
    internal struct RoutePattern
    {
        /// <summary>
        /// Gets the list of IReadOnlyList&lt;RoutePatternPathSegment&gt;
        /// </summary>
        public IEnumerable PathSegments;

        /// <summary>
        /// Gets the RoutePattern.RawText
        /// </summary>
        public string? RawText;
    }
}
