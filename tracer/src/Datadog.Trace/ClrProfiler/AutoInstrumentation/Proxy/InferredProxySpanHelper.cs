// <copyright file="InferredProxySpanHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Static helper class that provides the main entry points for creating inferred proxy spans.
/// Configures and manages the <see cref="InferredProxyCoordinator"/> instance.
/// </summary>
internal static class InferredProxySpanHelper
{
    private static readonly InferredProxyCoordinator Coordinator = new(new AwsApiGatewayExtractor(), new AwsApiGatewaySpanFactory());

    /// <summary>
    /// Creates an inferred proxy span from request headers.
    /// </summary>
    /// <param name="tracer">The <see cref="Tracer"/> instance.</param>
    /// <param name="carrier">The headers to extract.</param>
    /// <param name="propagationContext">The currently extracted <see cref="PropagationContext"/> from the request headers.</param>
    /// <returns>Created <see cref="Scope"/> and updated <see cref="PropagationContext"/> when successful.</returns>
    public static InferredProxyScopePropagationContext? ExtractAndCreateInferredProxyScope<THeadersCollection>(
        Tracer tracer,
        THeadersCollection carrier,
        PropagationContext propagationContext)
        where THeadersCollection : struct, IHeadersCollection
    {
        return Coordinator.ExtractAndCreateScope(tracer, carrier, carrier.GetAccessor(), propagationContext);
    }
}
