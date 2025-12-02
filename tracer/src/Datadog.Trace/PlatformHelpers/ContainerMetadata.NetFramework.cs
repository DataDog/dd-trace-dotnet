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
internal static class ContainerMetadata
{
    /// <summary>
    /// Gets or sets the container tags hash received from the agent, used by DBM/DSM
    /// </summary>
    public static string? ContainerTagsHash { get; set; }

    /// <summary>
    /// Gets the id of the container executing the code.
    /// Return <c>null</c> if code is not executing inside a supported container.
    /// </summary>
    /// <returns>The container id or <c>null</c>.</returns>
    public static string? GetContainerId() => null;

    /// <summary>
    /// Gets the unique identifier of the container executing the code.
    /// Return values may be:
    /// <list type="bullet">
    /// <item>"ci-&lt;containerID&gt;" if the container id is available.</item>
    /// <item>"in-&lt;inode&gt;" if the cgroup node controller's inode is available.
    ///        We use the memory controller on cgroupv1 and the root cgroup on cgroupv2.</item>
    /// <item><c>null</c> if neither are available.</item>
    /// </list>
    /// </summary>
    /// <returns>The entity id or <c>null</c>.</returns>
    public static string? GetEntityId() => null;
}
#endif
