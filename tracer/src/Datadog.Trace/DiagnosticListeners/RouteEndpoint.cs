// <copyright file="RouteEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Endpoint for duck typing
    /// </summary>
    [DuckCopy]
    public struct RouteEndpoint
    {
        /// <summary>
        /// Delegates to Endpoint.RoutePattern;
        /// </summary>
        public RoutePattern RoutePattern;

        /// <summary>
        /// Delegates to Endpoint.DisplayName;
        /// </summary>
        public string DisplayName;
    }
}
