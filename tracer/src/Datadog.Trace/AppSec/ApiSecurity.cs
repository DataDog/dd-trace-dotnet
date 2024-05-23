// <copyright file="ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.AppSec;

internal class ApiSecurity
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiSecurity>();
    private readonly int _maxRoutesSize;
    private readonly bool _enabled;
    private readonly TimeSpan _minTimeBetweenReprocessTimeSpan;
    private readonly OrderedDictionary _processedRoutes = new();
    private readonly Dictionary<string, bool> _apiSecurityAddress = new() { { "extract-schema", true } };

    public ApiSecurity(SecuritySettings securitySettings, int maxRouteSize = 4096)
    {
        // todo: later, will be enabled by default, depending on if Security is enabled
        _enabled = securitySettings.ApiSecurityEnabled;
        _minTimeBetweenReprocessTimeSpan = TimeSpan.FromSeconds(securitySettings.ApiSecuritySampleDelay);
        _maxRoutesSize = maxRouteSize;
    }

    public bool ShouldAnalyzeSchema(bool lastWafCall, Span localRootSpan, IDictionary<string, object> args, string? statusCode, IDictionary<string, object>? routeValues)
    {
        try
        {
            var samplingPriority = localRootSpan.Context.TraceContext.GetOrMakeSamplingDecision();

            if (_enabled && lastWafCall && SamplingPriorityValues.IsKeep(samplingPriority))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CombineHashes(string httpRouteTag, string httpMethod, string statusCode) => HashCode.Combine(httpRouteTag.GetHashCode(), httpMethod.GetHashCode(), statusCode.GetHashCode());
}
