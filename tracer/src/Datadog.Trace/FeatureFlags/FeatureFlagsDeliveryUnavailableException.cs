// <copyright file="FeatureFlagsDeliveryUnavailableException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Thrown from initialization when no source could start requesting flag configuration, so none will
/// ever arrive. Initialization reports this rather than returning, because a normal return tells the
/// SDK that the provider is ready while every evaluation would keep returning its default value.
/// </summary>
internal sealed class FeatureFlagsDeliveryUnavailableException : Exception
{
    public FeatureFlagsDeliveryUnavailableException(string? reason)
        : base($"Feature Flags cannot request configuration: {reason ?? "no delivery source could be started"}. Evaluations return their default values.")
    {
    }
}
