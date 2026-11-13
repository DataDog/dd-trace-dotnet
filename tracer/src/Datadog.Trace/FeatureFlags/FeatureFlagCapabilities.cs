// <copyright file="FeatureFlagCapabilities.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.FeatureFlags;

internal static class FeatureFlagCapabilities
{
    internal const string DependentFlagVariantKeyV1 = "dependent-flag-variant-key-v1";

    internal static string? HeaderValue(IReadOnlyCollection<string> capabilities)
        => capabilities.Contains(DependentFlagVariantKeyV1) ? DependentFlagVariantKeyV1 : null;

    internal static BigInteger RemoteConfigMask(IReadOnlyCollection<string> capabilities)
        => RcmCapabilitiesIndices.FfeFlagConfigurationRules
         | (capabilities.Contains(DependentFlagVariantKeyV1) ? RcmCapabilitiesIndices.FfeFlagDependentFlagVariantKeyV1 : BigInteger.Zero);
}
