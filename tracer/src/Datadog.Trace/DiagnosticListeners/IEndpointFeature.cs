// <copyright file="IEndpointFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Proxy for ducktyping IEndpointFeature when the interface is implemented
    /// explicitly, e.g. by https://github.com/dotnet/aspnetcore/blob/v3.0.3/src/Servers/Kestrel/Core/src/Internal/Http/HttpProtocol.FeatureCollection.cs
    /// Also see AspNetCoreDiagnosticObserver.EndpointFeaturesTruct
    /// </summary>
    public interface IEndpointFeature
    {
        /// <summary>
        /// Delegates to IEndpointFeature.Endpoint;
        /// </summary>
        [Duck(Name = "Microsoft.AspNetCore.Http.Features.IEndpointFeature.get_Endpoint")]
        RouteEndpoint GetEndpoint();
    }
}
