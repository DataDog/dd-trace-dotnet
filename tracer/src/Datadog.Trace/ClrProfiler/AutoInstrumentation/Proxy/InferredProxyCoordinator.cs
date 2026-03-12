// <copyright file="InferredProxyCoordinator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Coordinates the extraction of proxy metadata and creation of proxy spans.
/// Acts as the main entry point for creating inferred proxy spans.
/// </summary>
internal sealed class InferredProxyCoordinator
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<InferredProxyCoordinator>();
    private readonly IInferredSpanFactory _spanFactory;
    private readonly IInferredProxyExtractor _extractor;

    /// <summary>
    /// Initializes a new instance of the <see cref="InferredProxyCoordinator"/> class.
    /// </summary>
    /// <param name="extractor">Component that extracts proxy metadata from headers</param>
    /// <param name="spanFactory">Component that creates spans from the extracted metadata</param>
    public InferredProxyCoordinator(IInferredProxyExtractor extractor, IInferredSpanFactory spanFactory)
    {
        _spanFactory = spanFactory;
        _extractor = extractor;
    }

    /// <summary>
    /// Extracts proxy metadata from headers and creates a corresponding span.
    /// </summary>
    /// <returns>
    /// When successful, returns an object containing:
    /// <list type="bullet">
    /// <item>The created <see cref="Scope"/> containing the proxy <see cref="Span"/></item>
    /// <item>An updated <see cref="PropagationContext"/> that includes the new <see cref="SpanContext"/> and previous <see cref="Baggage"/>.</item>
    /// </list>
    /// Returns <see langword="null"/> if extraction fails or span creation fails.
    /// </returns>
    public InferredProxyScopePropagationContext? ExtractAndCreateScope<TCarrier, TCarrierGetter>(
        Tracer tracer,
        TCarrier carrier,
        TCarrierGetter carrierGetter,
        PropagationContext propagationContext)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        try
        {
            if (!_extractor.TryExtract(carrier, carrierGetter, out var proxyData))
            {
                return null;
            }

            var scope = _spanFactory.CreateSpan(tracer, proxyData, propagationContext.SpanContext);
            if (scope == null)
            {
                return null;
            }

            var updatedContext = new PropagationContext(scope.Span.Context, propagationContext.Baggage);
            return new InferredProxyScopePropagationContext(scope, updatedContext);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing inferred proxy span.");
            return null;
        }
    }
}
