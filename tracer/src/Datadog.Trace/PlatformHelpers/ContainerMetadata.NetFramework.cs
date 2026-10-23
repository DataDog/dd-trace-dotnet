// <copyright file="ContainerMetadata.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

#if NETFRAMEWORK

namespace Datadog.Trace.PlatformHelpers;

/// <summary>
/// Utility class with methods to interact with container hosts.
/// </summary>
internal sealed class ContainerMetadata
{
    public static readonly ContainerMetadata Instance = new();

    private readonly Lazy<string?> _externalEnv;

    private ContainerMetadata()
    {
        _externalEnv = new Lazy<string?>(ContainerMetadataHelper.GetExternalEnvFromEnvironment, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    // For use in tests only
    [TestingOnly]
    public ContainerMetadata(string? containerId, string? entityId, string? externalEnv)
    {
        // containerId and entityId are not supported on .NET Framework, they're only here to match the other version
        _externalEnv = new Lazy<string?>(() => ContainerMetadataHelper.NormalizeExternalEnv(externalEnv));
    }

    /// <summary>
    /// Gets the id of the container executing the code.
    /// Return <c>null</c> if code is not executing inside a supported container.
    /// </summary>
    /// <value>The container id or <c>null</c>.</value>
    public string? ContainerId => null;

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
    public string? EntityId => null;

    /// <summary>
    /// Gets the origin detection information injected by the Datadog Cluster Agent Admission Controller
    /// in the <c>DD_EXTERNAL_ENV</c> environment variable,
    /// e.g. <c>it-false,cn-nginx-webserver,pu-75a2b6d5-3949-4afb-ad0d-92ff0674e759</c>.
    /// Return <c>null</c> if the variable is not set, is empty, or its value cannot be sent in an HTTP header.
    /// </summary>
    /// <value>The external env or <c>null</c>.</value>
    public string? ExternalEnv => _externalEnv.Value;
}
#endif
