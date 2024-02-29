// <copyright file="ApiSecurity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec;

internal class ApiSecurity
{
    private const int MinTimeBetweenReprocess = 30;
    private const int MaxRoutesSize = 4096;
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ApiSecurity>();
    private readonly bool _enabled;
    private readonly TimeSpan _minTimeBetweenReprocessTimeSpan = TimeSpan.FromSeconds(MinTimeBetweenReprocess);
    private readonly IDictionary<int, DateTime> _processedRoutes = new Dictionary<int, DateTime>();
    private readonly Dictionary<string, bool> _apiSecurityAddress = new() { { "extract-schema", true } };

    public ApiSecurity(SecuritySettings securitySettings)
    {
        // todo: later, will be enabled by default, depending on if Security is enabled
        _enabled = securitySettings.ApiSecurityEnabled;
    }

    public bool ShouldAnalyzeSchema(bool lastWafCall, Span localRootSpan, IDictionary<string, object> args)
    {
        if (_enabled && lastWafCall && localRootSpan.Context.SamplingPriority > (int?)SamplingPriority.AutoReject)
        {
            var httpRouteTag = localRootSpan.GetTag(Tags.HttpRoute);
            var httpMethod = localRootSpan.GetTag(Tags.HttpMethod);
            var statusCode = localRootSpan.GetTag(Tags.HttpStatusCode);
            if (httpRouteTag == null || httpMethod == null || statusCode == null)
            {
                Log.Debug("Unsupported groupkey for api security {Route}, {Method}, {Status}", httpRouteTag, httpMethod, statusCode);
                return false;
            }

            var hash = HashCode.Combine(httpRouteTag.GetHashCode(), httpMethod.GetHashCode(), statusCode.GetHashCode());
            var now = DateTime.UtcNow;
            if (_processedRoutes.TryGetValue(hash, out var lastProcessed))
            {
                if (lastProcessed - now > _minTimeBetweenReprocessTimeSpan)
                {
                    args.Add(AddressesConstants.WafContextProcessor, _apiSecurityAddress);
                    _processedRoutes[hash] = now;
                    return true;
                }
            }
            else
            {
                // shouldn't happen so often (that's a lot of routes)
                if (_processedRoutes.Count == MaxRoutesSize)
                {
                    var oldest = _processedRoutes.OrderBy(x => x.Value).First();
                    _processedRoutes.Remove(oldest);
                }

                args.Add(AddressesConstants.WafContextProcessor, _apiSecurityAddress);
                _processedRoutes.Add(hash, now);

                return true;
            }
        }

        return false;
    }
}
