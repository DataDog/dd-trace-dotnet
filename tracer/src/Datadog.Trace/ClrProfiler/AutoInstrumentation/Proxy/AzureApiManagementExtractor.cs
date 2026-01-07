// <copyright file="AzureApiManagementExtractor.cs" company="Datadog">
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
/// Extracts proxy metadata from Azure API Management headers.
/// </summary>
internal sealed class AzureApiManagementExtractor : IInferredProxyExtractor
{
    // This is the expected value of the x-dd-proxy header
    private const string ProxyName = "azure-apim";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureApiManagementExtractor>();

    public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out InferredProxyData data)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        data = default;

        try
        {
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
            var region = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Region);
            data = new InferredProxyData(ProxyName, startTime, domainName, httpMethod, path, null);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Successfully extracted proxy data: StartTime={StartTime}, Domain={Domain}, Method={Method}, Path={Path}, Stage={Stage}",
                    [startTimeHeaderValue, domainName, httpMethod, path, region]);
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

        // Parse as ISO 8601 timestamp (e.g., "2025-12-03T14:21:01.1900116Z")
        if (!DateTimeOffset.TryParse(startTime, out var parsedTime))
        {
            Log.Warning("Failed to parse header '{HeaderName}' with value '{Value}'", InferredProxyHeaders.StartTime, startTime);
            return false;
        }

        start = parsedTime;
        return true;
    }
}
