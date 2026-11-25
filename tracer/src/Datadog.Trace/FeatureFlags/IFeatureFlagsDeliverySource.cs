// <copyright file="IFeatureFlagsDeliverySource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.FeatureFlags.Agentless;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// A source of flag configuration that the module starts on activation and disposes with itself.
/// </summary>
internal interface IFeatureFlagsDeliverySource : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the current request leaves out the flag selection because the
    /// request URL would be too long with it.
    /// </summary>
    bool OmitsFlagSelection { get; }

    /// <summary>
    /// Starts requesting configuration. Idempotent.
    /// </summary>
    void Start();

    /// <summary>
    /// Requests configuration for a newly claimed flag selection, from the next request on.
    /// </summary>
    /// <param name="selection">The claimed selection.</param>
    void UpdateFlagSelection(FlagSelection selection);
}
