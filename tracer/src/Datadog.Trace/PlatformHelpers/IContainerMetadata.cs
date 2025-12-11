// <copyright file="IContainerMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Agent;

namespace Datadog.Trace.PlatformHelpers;

internal interface IContainerMetadata
{
    /// <summary>
    /// Gets the id of the container executing the code.
    /// Return <c>null</c> if code is not executing inside a supported container.
    /// </summary>
    /// <value>The container id or <c>null</c>.</value>
    public string? ContainerId { get; }

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
    /// <value>The entity id or <c>null</c>.</value>
    public string? EntityId { get; }
}

internal static class ContainerMetadataHelper
{
    public static void AddContainerMetadataHeaders(this IApiRequest request, IContainerMetadata containerMetadata)
    {
        // Set additional headers
        if (containerMetadata.ContainerId != null)
        {
            request.AddHeader(AgentHttpHeaderNames.ContainerId, containerMetadata.ContainerId);
        }

        if (containerMetadata.EntityId != null)
        {
            request.AddHeader(AgentHttpHeaderNames.EntityId, containerMetadata.EntityId);
        }
    }
}
