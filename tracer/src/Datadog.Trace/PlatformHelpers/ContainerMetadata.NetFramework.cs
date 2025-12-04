// <copyright file="ContainerMetadata.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

namespace Datadog.Trace.PlatformHelpers;

/// <summary>
/// Utility class with methods to interact with container hosts.
/// </summary>
internal sealed class ContainerMetadata : IContainerMetadata
{
    public static readonly IContainerMetadata Instance = new ContainerMetadata();

    /// <inheritdoc/>
    public string? ContainerId => null;

    /// <inheritdoc/>
    public string? EntityId => null;
}
#endif
