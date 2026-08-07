// <copyright file="FeatureFlagsSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// Where flag configuration is loaded from.
/// </summary>
internal enum FeatureFlagsSource
{
    /// <summary>
    /// Feature Flags are disabled: no configuration is loaded, and neither delivery path is contacted.
    /// </summary>
    Disabled,

    /// <summary>
    /// Configuration is fetched over HTTP, without the Datadog Agent.
    /// </summary>
    Agentless,

    /// <summary>
    /// Configuration is delivered through the Datadog Agent's Remote Configuration.
    /// </summary>
    RemoteConfig,
}
