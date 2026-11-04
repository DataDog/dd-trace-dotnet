// <copyright file="FeatureFlagCapabilities.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.FeatureFlags;

internal static class FeatureFlagCapabilities
{
    internal const string DependentFlagVariantKeyV1 = "dependent-flag-variant-key-v1";
    private const string DependentFlagsVariantKeyQueryName = "cap_dependent-flags-variant-key";
    private const string DependentFlagsVariantKeyQuery = "cap_dependent-flags-variant-key=v1";

    internal static Uri AddToUri(Uri uri, IReadOnlyCollection<string> capabilities)
    {
        if (!capabilities.Contains(DependentFlagVariantKeyV1))
        {
            return uri;
        }

        var builder = new UriBuilder(uri);
        var query = builder.Query.Length == 0 ? new List<string>() : builder.Query.Substring(1).Split('&').ToList();
        query.RemoveAll(
            item => item.Equals(DependentFlagsVariantKeyQueryName, StringComparison.Ordinal)
                 || item.StartsWith(DependentFlagsVariantKeyQueryName + "=", StringComparison.Ordinal));
        query.Add(DependentFlagsVariantKeyQuery);
        builder.Query = string.Join("&", query);
        return builder.Uri;
    }

    internal static BigInteger RemoteConfigMask(IReadOnlyCollection<string> capabilities)
        => RcmCapabilitiesIndices.FfeFlagConfigurationRules
         | (capabilities.Contains(DependentFlagVariantKeyV1) ? RcmCapabilitiesIndices.FfeFlagDependentFlagVariantKeyV1 : BigInteger.Zero);
}
