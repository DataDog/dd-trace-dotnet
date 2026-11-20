// <copyright file="RemoteConfigurationChanges.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.RemoteConfigurationManagement;

internal sealed class RemoteConfigurationChanges
{
    public RemoteConfigurationChanges(List<RemoteConfiguration> updated, List<RemoteConfigurationPath> removed)
    {
        Updated = updated;
        Removed = removed;
    }

    /// <summary>
    /// Gets the added or updated configurations.
    /// </summary>
    public List<RemoteConfiguration> Updated { get; }

    public List<RemoteConfigurationPath> Removed { get; }

    public bool IsEmpty => Updated.Count == 0 && Removed.Count == 0;
}
