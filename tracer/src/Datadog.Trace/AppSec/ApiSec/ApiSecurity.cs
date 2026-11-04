// <copyright file="ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.AppSec;

internal sealed class ApiSecurity
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiSecurity>();

    private readonly int _maxRoutesSize;
    private readonly bool _enabled;
    private readonly bool _apmTracingEnabled;
    private readonly bool _endpointsCollectionEnabled;
    private readonly int _endpointsCollectionMessageLimit;
    private readonly TimeSpan _minTimeBetweenReprocessTimeSpan;
    private readonly OrderedDictionary _processedRoutes = new();
    private readonly Dictionary<string, bool> _apiSecurityAddress = new() { { "extract-schema", true } };

    public ApiSecurity(SecuritySettings securitySettings, int maxRouteSize = 4096)
    {
        // todo: later, will be enabled by default, depending on if Security is enabled
        _enabled = securitySettings.ApiSecurityEnabled;
        _apmTracingEnabled = securitySettings.ApmTracingEnabled;
        _minTimeBetweenReprocessTimeSpan = TimeSpan.FromSeconds(securitySettings.ApiSecuritySampleDelay);
        _maxRoutesSize = maxRouteSize;
        _endpointsCollectionEnabled = securitySettings is { ApiSecurityEndpointCollectionEnabled: true, AppsecEnabled: true };
        _endpointsCollectionMessageLimit = securitySettings.ApiSecurityEndpointCollectionMessageLimit;
    }

    internal enum SamplingDecision
    {
        Skip,
        Sample,

        /// <summary>
        /// The request has no route, so it can't be sampled. It is reported once the last WAF run tells whether it was blocked.
        /// </summary>
        MissingRoute,
    }

    public bool ShouldAnalyzeSchema(bool lastWafCall, Span localRootSpan, IDictionary<string, object> args, int? statusCode, IDictionary<string, object>? routeValues)
        => GetSamplingDecision(lastWafCall, localRootSpan, args, statusCode, routeValues) == SamplingDecision.Sample;

    internal SamplingDecision GetSamplingDecision(bool lastWafCall, Span localRootSpan, IDictionary<string, object> args, int? statusCode, IDictionary<string, object>? routeValues)
    {
        try
        {
            var samplingPriority = localRootSpan.Context.TraceContext.GetOrMakeSamplingDecision();

            if (_enabled && lastWafCall && (!_apmTracingEnabled || SamplingPriorityValues.IsKeep(samplingPriority)))
            {
                var httpRouteTag = localRootSpan.GetTag(Tags.AspNetCoreEndpoint) ?? localRootSpan.GetTag(Tags.HttpRoute);
                var httpMethod = localRootSpan.GetHttpMethod();
                statusCode ??= localRootSpan.GetHttpStatusCode();
                if (httpMethod == null || statusCode == null)
                {
                    Log.Debug("Unsupported groupkey for api security {Route}, {Method}, {Status}", httpRouteTag, httpMethod, statusCode);
                    return SamplingDecision.Skip;
                }

                if (httpRouteTag == null)
                {
                    Log.Debug("No route for api security, {Method}, {Status}", httpMethod, statusCode);

                    // a request that wasn't found or that was blocked has no route to miss
                    return statusCode != 404 && localRootSpan.GetTag(Tags.AppSecBlocked) != "true" ? SamplingDecision.MissingRoute : SamplingDecision.Skip;
                }

                if (routeValues != null)
                {
                    foreach (var routeValue in routeValues)
                    {
                        var routeKey = $"{{{routeValue.Key}}}";
                        if (routeValue.Value is string && httpRouteTag.Contains(routeKey))
                        {
                            httpRouteTag = httpRouteTag.Replace(routeKey, routeValue.Value.ToString());
                        }
                    }
                }

                var hash = CombineHashes(httpRouteTag, httpMethod, statusCode);
                var now = DateTime.UtcNow;
                if (_processedRoutes.TryGetValue<DateTime>(hash, out var lastProcessed))
                {
                    if (now - lastProcessed > _minTimeBetweenReprocessTimeSpan)
                    {
                        lock (_apiSecurityAddress)
                        {
                            _processedRoutes.Remove(hash);
                            _processedRoutes.Add(hash, now);
                        }

                        args[AddressesConstants.WafContextProcessor] = _apiSecurityAddress;
                        return SamplingDecision.Sample;
                    }
                }
                else
                {
                    lock (_apiSecurityAddress)
                    {
                        // it's full, remove the oldest, as it's an ordered dic, insertion order is preserved
                        if (_processedRoutes.Count == _maxRoutesSize)
                        {
                            _processedRoutes.RemoveAt(0);
                        }

                        if (!_processedRoutes.Contains(hash))
                        {
                            _processedRoutes.Add(hash, now);
                        }
                        else
                        {
                            return SamplingDecision.Skip;
                        }
                    }

                    args[AddressesConstants.WafContextProcessor] = _apiSecurityAddress;
                    return SamplingDecision.Sample;
                }
            }

            return SamplingDecision.Skip;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in GetSamplingDecision");
            return SamplingDecision.Skip;
        }
    }

    /// <summary>
    /// Checks if the Endpoints collection is enabled.
    /// Note that this feature can be run on its own without Appsec nor API Security being enabled.
    /// </summary>
    /// <returns> bool value </returns>
    public bool CanCollectEndpoints() => _endpointsCollectionEnabled;

    /// <summary>
    /// Records the outcome of a sampling decision once the WAF ran: whether a sampled request got a schema,
    /// and whether a request without route was blocked by that last run
    /// </summary>
    internal static void RecordSamplingTelemetry(SamplingDecision decision, IResult? result, Span localRootSpan, IMetricsTelemetryCollector metrics)
    {
        switch (decision)
        {
            case SamplingDecision.Sample when result?.ExtractSchemaDerivatives is { Count: > 0 }:
                metrics.RecordCountApiSecRequestSchema(GetFramework(localRootSpan));
                break;
            case SamplingDecision.Sample:
                metrics.RecordCountApiSecRequestNoSchema(GetFramework(localRootSpan));
                break;
            case SamplingDecision.MissingRoute when result?.ShouldBlock is not true:
                metrics.RecordCountApiSecMissingRoute(GetFramework(localRootSpan));
                break;
        }
    }

    public int GetEndpointsCollectionMessageLimit() => _endpointsCollectionMessageLimit;

    private static MetricTags.ApiSecFramework GetFramework(Span localRootSpan)
        => localRootSpan.GetTag(Tags.InstrumentationName) switch
        {
            "aspnet_core" => MetricTags.ApiSecFramework.AspNetCore,
            "aspnet" => MetricTags.ApiSecFramework.AspNet,
            _ => MetricTags.ApiSecFramework.Unknown,
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CombineHashes(string httpRouteTag, string httpMethod, int? statusCode) => HashCode.Combine(httpRouteTag.GetHashCode(), httpMethod.GetHashCode(), statusCode?.GetHashCode());
}
