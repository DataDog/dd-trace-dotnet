// <copyright file="RouteEndpoint.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners;

/// <summary>
/// Endpoint for duck typing
/// </summary>
[DuckCopy]
internal struct RouteEndpoint
{
    /// <summary>
    /// Delegates to Endpoint.RoutePattern;
    /// </summary>
    public object RoutePattern;

    /// <summary>
    /// Delegates to Endpoint.DisplayName;
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// Delegates to Endpoint.RequestDelegate;
    /// </summary>
    public RequestDelegate? RequestDelegate;

    /// <summary>
    /// Delegates to Endpoint.Metadata;
    /// </summary>
    public object Metadata;
}
