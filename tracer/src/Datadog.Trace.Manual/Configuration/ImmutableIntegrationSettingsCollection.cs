﻿// <copyright file="ImmutableIntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// A collection of <see cref="ImmutableIntegrationSettings"/> instances, referenced by name.
/// </summary>
public sealed class ImmutableIntegrationSettingsCollection
{
    internal ImmutableIntegrationSettingsCollection(Dictionary<string, ImmutableIntegrationSettings> settings)
    {
        Settings = settings;
    }

    internal Dictionary<string, ImmutableIntegrationSettings> Settings { get; }

    /// <summary>
    /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
    /// </summary>
    /// <param name="integrationName">The name of the integration.</param>
    /// <returns>The integration-specific settings for the specified integration.</returns>
    public ImmutableIntegrationSettings this[string integrationName]
    {
        [Instrumented]
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            if (Settings.TryGetValue(integrationName, out var setting))
            {
                return setting;
            }

            // Unknown integration or manual-only instrumentation
            // so return a "null" version.
            return new ImmutableIntegrationSettings(integrationName);
        }
    }
}
