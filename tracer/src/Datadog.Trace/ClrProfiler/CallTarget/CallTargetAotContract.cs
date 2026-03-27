// <copyright file="CallTargetAotContract.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.CallTarget;

/// <summary>
/// Describes the generator/runtime compatibility contract for a generated CallTarget NativeAOT registry.
/// </summary>
internal readonly struct CallTargetAotContract
{
    /// <summary>
    /// Defines the schema version understood by the current runtime.
    /// </summary>
    internal const string CurrentSchemaVersion = "1";

    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotContract"/> struct.
    /// </summary>
    /// <param name="schemaVersion">The generated contract schema version.</param>
    /// <param name="datadogTraceAssemblyVersion">The Datadog.Trace assembly version used during generation.</param>
    /// <param name="datadogTraceAssemblyMvid">The Datadog.Trace module MVID used during generation.</param>
    internal CallTargetAotContract(string schemaVersion, string datadogTraceAssemblyVersion, string datadogTraceAssemblyMvid)
    {
        SchemaVersion = schemaVersion;
        DatadogTraceAssemblyVersion = datadogTraceAssemblyVersion;
        DatadogTraceAssemblyMvid = datadogTraceAssemblyMvid;
    }

    /// <summary>
    /// Gets the generated contract schema version.
    /// </summary>
    internal string SchemaVersion { get; }

    /// <summary>
    /// Gets the Datadog.Trace assembly version used during generation.
    /// </summary>
    internal string DatadogTraceAssemblyVersion { get; }

    /// <summary>
    /// Gets the Datadog.Trace module MVID used during generation.
    /// </summary>
    internal string DatadogTraceAssemblyMvid { get; }
}

/// <summary>
/// Describes the identity of the generated CallTarget NativeAOT registry assembly.
/// </summary>
internal readonly struct CallTargetAotAssemblyMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallTargetAotAssemblyMetadata"/> struct.
    /// </summary>
    /// <param name="registryAssemblyFullName">The generated registry assembly full name.</param>
    /// <param name="registryAssemblyMvid">The generated registry module MVID.</param>
    internal CallTargetAotAssemblyMetadata(string registryAssemblyFullName, string registryAssemblyMvid)
    {
        RegistryAssemblyFullName = registryAssemblyFullName;
        RegistryAssemblyMvid = registryAssemblyMvid;
    }

    /// <summary>
    /// Gets the generated registry assembly full name.
    /// </summary>
    internal string RegistryAssemblyFullName { get; }

    /// <summary>
    /// Gets the generated registry module MVID.
    /// </summary>
    internal string RegistryAssemblyMvid { get; }

    /// <summary>
    /// Gets the normalized registry assembly identity used by runtime validation.
    /// </summary>
    internal string RegistryAssemblyIdentity => $"{RegistryAssemblyFullName}; MVID={RegistryAssemblyMvid}";
}
