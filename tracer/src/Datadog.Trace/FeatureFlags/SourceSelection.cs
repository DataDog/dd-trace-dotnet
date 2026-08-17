// <copyright file="SourceSelection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// A wrapper used as the converter result type so the config framework records telemetry,
/// while allowing <see cref="FeatureFlagsSettings"/> to distinguish "not set" (null) from
/// "set to an invalid value" (non-null with <see cref="FeatureFlagsSource.Disabled"/>).
/// </summary>
internal sealed class SourceSelection(FeatureFlagsSource source, bool isValid)
{
    public FeatureFlagsSource Source { get; } = source;

    public bool IsValid { get; } = isValid;
}
