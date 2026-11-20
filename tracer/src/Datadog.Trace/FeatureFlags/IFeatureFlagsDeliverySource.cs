// <copyright file="IFeatureFlagsDeliverySource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.FeatureFlags;

/// <summary>
/// A source of flag configuration that the module starts on activation and disposes with itself.
/// </summary>
internal interface IFeatureFlagsDeliverySource : IDisposable
{
    /// <summary>
    /// Starts requesting configuration. Idempotent.
    /// </summary>
    void Start();
}
