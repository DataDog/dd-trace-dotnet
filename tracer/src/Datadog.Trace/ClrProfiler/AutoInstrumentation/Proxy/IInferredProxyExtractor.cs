// <copyright file="IInferredProxyExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Supports extracting inferred proxy data from request headers.
/// </summary>
internal interface IInferredProxyExtractor
{
    /// <summary>
    /// Attempts to extract proxy metadata from the given carrier (e.g. HTTP headers).
    /// </summary>
    /// <typeparam name="TCarrier">The type containing the headers (e.g. <c>IHeadersCollection</c>)</typeparam>
    /// <typeparam name="TCarrierGetter">The type that can extract values from the carrier.</typeparam>
    /// <param name="carrier">The container of the headers.</param>
    /// <param name="carrierGetter">Helper to extract values from the carrier.</param>
    /// <param name="tracer">The <see cref="Tracer"/> instance.</param>
    /// <param name="data">When sucessful, contains the extracted proxy metadata.</param>
    /// <returns><see langword="true"/> if extraction was successful; otherwise, <see langword="false"/>. </returns>
    bool TryExtract<TCarrier, TCarrierGetter>(
        TCarrier carrier,
        TCarrierGetter carrierGetter,
        out InferredProxyData data)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>;
}
