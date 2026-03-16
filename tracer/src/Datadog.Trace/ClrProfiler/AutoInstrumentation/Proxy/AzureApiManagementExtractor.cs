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
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AzureApiManagementExtractor>();

    public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out InferredProxyData data)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        data = default;

        try
        {
            var startTimeHeaderValue = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.StartTime);

            // we also need to validate that we have the start time header otherwise we won't be able to create the span
            if (!InferredProxySpanHelper.GetStartTime(startTimeHeaderValue, out var startTime))
            {
                return false;
            }

            // the remaining headers aren't necessarily required
            var domainName = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Domain);
            var httpMethod = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.HttpMethod);
            var path = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Path);
            var region = ParseUtility.ParseString(carrier, carrierGetter, InferredProxyHeaders.Region);
            data = new InferredProxyData(InferredProxySpanHelper.AzureProxyHeaderValue, startTime, domainName, httpMethod, path, null, region);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Successfully extracted proxy data: StartTime={StartTime}, Domain={Domain}, Method={Method}, Path={Path}, Region={Region}",
                    [startTimeHeaderValue, domainName, httpMethod, path, region]);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error extracting proxy data from {Proxy} headers", InferredProxySpanHelper.AzureProxyHeaderValue);
            return false;
        }
    }
}
