// <copyright file="RoutePatternRequiredValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.AppSec.ApiSec.DuckType
{
    /// <summary>
    /// RoutePattern for duck typing
    /// </summary>
    [DuckCopy]
    internal struct RoutePatternRequiredValues
    {
        /// <summary>
        /// Gets the RoutePattern.RequiredValues
        /// </summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, object?> RequiredValues;
    }
}

#endif
