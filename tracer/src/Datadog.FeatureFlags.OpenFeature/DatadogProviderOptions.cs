// <copyright file="DatadogProviderOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>Options copied when a Datadog provider is constructed.</summary>
public sealed class DatadogProviderOptions
{
    /// <summary>
    /// Gets or sets the flags to load. Unset, every flag loads. The selection is copied when the provider
    /// is constructed, and the first provider constructed in a process decides the selection for every
    /// provider in that process until they have all been shut down or disposed.
    /// </summary>
    public FlagSelection? FlagSelection { get; set; }
}
