// <copyright file="InferredProxySpanHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Static helper class that provides the main entry points for creating inferred proxy spans.
/// Configures and manages the <see cref="InferredProxyCoordinator"/> instance.
/// </summary>
internal static class InferredProxySpanHelper
{
    public const string AzureProxyHeaderValue = "azure-apim";
    private const string AwsProxyHeaderValue = "aws-apigateway";
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InferredProxySpanHelper));
    private static InferredProxyCoordinator? _awsCoordinator;
    private static InferredProxyCoordinator? _azureCoordinator;

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
        var accessor = carrier.GetAccessor();
        var proxyName = ParseUtility.ParseString(carrier, accessor, InferredProxyHeaders.Name);

        if (string.IsNullOrEmpty(proxyName))
        {
            Log.Debug("Missing {HeaderName} header", InferredProxyHeaders.Name);
            return null;
        }

        if (string.Equals(proxyName, AzureProxyHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            _azureCoordinator ??= new InferredProxyCoordinator(new AzureApiManagementExtractor(), new AzureApiManagementSpanFactory());
            return _azureCoordinator.ExtractAndCreateScope(tracer, carrier, accessor, propagationContext);
        }

        if (string.Equals(proxyName, AwsProxyHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            _awsCoordinator ??= new InferredProxyCoordinator(new AwsApiGatewayExtractor(), new AwsApiGatewaySpanFactory());
            return _awsCoordinator.ExtractAndCreateScope(tracer, carrier, accessor, propagationContext);
        }

        Log.Debug("Invalid \"{HeaderName}\" header value: \"{Value}\"", InferredProxyHeaders.Name, proxyName);
        return null;
    }
}
