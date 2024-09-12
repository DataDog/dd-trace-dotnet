// <copyright file="RCMCapabilitiesHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Numerics;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;

#nullable enable

namespace Datadog.Trace.AppSec.Waf;

internal static class RCMCapabilitiesHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RCMCapabilitiesHelper));

    private static readonly Dictionary<BigInteger, Version> _CapabilitiesVersion = new Dictionary<BigInteger, Version>()
    {
        { RcmCapabilitiesIndices.AsmRaspSqli, new Version(1, 18) },
        { RcmCapabilitiesIndices.AsmRaspLfi, new Version(1, 17) },
        { RcmCapabilitiesIndices.AsmRaspSsrf, new Version(1, 17) },
        { RcmCapabilitiesIndices.AsmRaspShi, new Version(1, 19) },
        { RcmCapabilitiesIndices.AsmExclusionData, new Version(1, 19) },
    };

    internal static bool WafSupportsCapability(BigInteger capability, string? wafVersion)
    {
        var currentVersion = wafVersion;

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        if (_CapabilitiesVersion.TryGetValue(capability, out var version))
        {
            try
            {
                return Version.Parse(currentVersion) >= version;
            }
            catch
            {
                Log.Warning("Failed to parse WAF version {WafVersion}", currentVersion);
                return false;
            }
        }

        return false;
    }
}
