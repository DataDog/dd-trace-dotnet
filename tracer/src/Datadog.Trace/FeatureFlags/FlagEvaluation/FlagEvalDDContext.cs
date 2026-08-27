// <copyright file="FlagEvalDDContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Batch-level Datadog context (service/env/version).
/// </summary>
internal sealed class FlagEvalDDContext
{
    [JsonConstructor]
    public FlagEvalDDContext(string service, string? env, string? version)
    {
        Service = service;
        Env = env;
        Version = version;
    }

    /// <summary>Gets the service name.</summary>
    public string Service { get; }

    /// <summary>Gets the environment.</summary>
    public string? Env { get; }

    /// <summary>Gets the service version.</summary>
    public string? Version { get; }
}
