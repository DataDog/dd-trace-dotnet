// <copyright file="ConfigurationApplyResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// What became of a configuration the agentless source handed to the module.
/// </summary>
internal enum ConfigurationApplyResult
{
    /// <summary>The configuration is installed.</summary>
    Applied,

    /// <summary>The configuration could not be installed; the previous one remains.</summary>
    Failed,

    /// <summary>
    /// The configuration was requested for a flag selection that is no longer claimed, so it was
    /// discarded. Nothing failed: the source already requests the claimed selection.
    /// </summary>
    Superseded,
}
