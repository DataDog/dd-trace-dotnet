// <copyright file="AwsApiGatewayExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;

/// <summary>
/// Extracts proxy metadata from AWS API Gateway headers.
/// </summary>
internal sealed class AwsApiGatewayExtractor : IInferredProxyExtractor
{
    // This is the expected value of the x-dd-proxy header
    private const string ProxyName = "aws-apigateway";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AwsApiGatewayExtractor>();

    public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, Tracer tracer, out InferredProxyData data)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        data = default;

        try
        {
            if (tracer?.Settings.InferredProxySpansEnabled != true)
            {
                return false;
            }

            // we need to first validate whether or not we have the header and whether or not it matches aws-apigateway
            var proxyName = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Name);
            if (proxyName is null || !string.Equals(proxyName, ProxyName, StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("Invalid or missing {HeaderName} header with value {Value}", InferredProxyHeaders.Name, proxyName);
                return false;
            }

            var startTimeHeaderValue = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.StartTime);

            // we also need to validate that we have the start time header otherwise we won't be able to create the span
            if (!GetStartTime(startTimeHeaderValue, out var startTime))
            {
                return false;
            }

            // the remaining headers aren't necessarily required
            var domainName = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Domain);
            var httpMethod = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.HttpMethod);
            var path = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Path);
            var stage = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Stage);

            data = new InferredProxyData(proxyName, startTime, domainName, httpMethod, path, stage);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Successfully extracted proxy data: StartTime={StartTime}, Domain={Domain}, Method={Method}, Path={Path}, Stage={Stage}",
                    [startTimeHeaderValue, domainName, httpMethod, path, stage]);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting proxy data from {Proxy} headers", ProxyName);
            return false;
        }
    }

    private static bool GetStartTime(string? startTime, out DateTimeOffset start)
    {
        start = default;

        if (string.IsNullOrEmpty(startTime))
        {
            Log.Debug("Missing header '{HeaderName}'", InferredProxyHeaders.StartTime);
            return false;
        }

        if (!long.TryParse(startTime, out var startTimeMs))
        {
            Log.Warning("Failed to parse header '{HeaderName}' with value '{Value}'", InferredProxyHeaders.StartTime, startTime);
            return false;
        }

        try
        {
            start = DateTimeOffset.FromUnixTimeMilliseconds(startTimeMs);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to convert value '{Value}' from header '{HeaderName}' to DateTimeOffset", InferredProxyHeaders.StartTime, startTimeMs);
            return false;
        }
    }
}
