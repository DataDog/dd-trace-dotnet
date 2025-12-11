// <copyright file="ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

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

    public bool ShouldAnalyzeSchema(bool lastWafCall, Span localRootSpan, IDictionary<string, object> args, string? statusCode, IDictionary<string, object>? routeValues)
    {
        try
        {
            var samplingPriority = localRootSpan.Context.TraceContext.GetOrMakeSamplingDecision();

            if (_enabled && lastWafCall && (!_apmTracingEnabled || SamplingPriorityValues.IsKeep(samplingPriority)))
            {
                var httpRouteTag = localRootSpan.GetTag(Tags.AspNetCoreEndpoint) ?? localRootSpan.GetTag(Tags.HttpRoute);
                var httpMethod = localRootSpan.GetTag(Tags.HttpMethod);
                statusCode ??= localRootSpan.GetTag(Tags.HttpStatusCode);
                if (httpRouteTag == null || httpMethod == null || statusCode == null)
                {
                    Log.Debug("Unsupported groupkey for api security {Route}, {Method}, {Status}", httpRouteTag, httpMethod, statusCode);
                    return false;
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
                        return true;
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
                            return false;
                        }
                    }

                    args[AddressesConstants.WafContextProcessor] = _apiSecurityAddress;
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ShouldAnalyzeSchema");
            return false;
        }
    }

    /// <summary>
    /// Checks if the Endpoints collection is enabled.
    /// Note that this feature can be run on its own without Appsec nor API Security being enabled.
    /// </summary>
    /// <returns> bool value </returns>
    public bool CanCollectEndpoints() => _endpointsCollectionEnabled;

    public int GetEndpointsCollectionMessageLimit() => _endpointsCollectionMessageLimit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CombineHashes(string httpRouteTag, string httpMethod, string statusCode) => HashCode.Combine(httpRouteTag.GetHashCode(), httpMethod.GetHashCode(), statusCode.GetHashCode());
}
