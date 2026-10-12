// <copyright file="IRemoteConfigClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;

namespace Datadog.Trace.RemoteConfigurationManagement;

/// <summary>
/// The remote configuration protocol client: polls the agent, validates and tracks the configurations.
/// Implemented by libdatadog, see <see cref="LibDatadog.RemoteConfig.NativeRemoteConfigClient"/>.
/// </summary>
internal interface IRemoteConfigClient : IDisposable
{
    void SetProducts(ICollection<string> products, BigInteger capabilities);

    void SetExtraServices(string[]? services);

    /// <summary>
    /// Switches to another target (service, env, version, tags) and/or agent endpoint, without
    /// downloading unchanged configurations again.
    /// </summary>
    void Rebind(RcmClientTracer target, RemoteConfigEndpoint? endpoint);

    /// <summary>
    /// Polls the agent once, blocking until the response is processed.
    /// </summary>
    RemoteConfigurationChanges Fetch();

    void SetApplyState(string path, ulong applyState, string? error);

    /// <summary>
    /// Aborts an ongoing fetch, and makes all further fetches fail. Can be called from any thread.
    /// </summary>
    void Cancel();
}
