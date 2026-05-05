// <copyright file="DuckTypeAotContract.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// Represents duck type aot contract.
    /// </summary>
    internal readonly struct DuckTypeAotContract
    {
        /// <summary>
        /// Defines the current schema version constant.
        /// </summary>
        internal const string CurrentSchemaVersion = "1";

        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotContract"/> struct.
        /// </summary>
        /// <param name="schemaVersion">The schema version value.</param>
        /// <param name="datadogTraceAssemblyVersion">The datadog trace assembly version value.</param>
        /// <param name="datadogTraceAssemblyMvid">The datadog trace assembly mvid value.</param>
        internal DuckTypeAotContract(string schemaVersion, string datadogTraceAssemblyVersion, string datadogTraceAssemblyMvid)
        {
            SchemaVersion = schemaVersion;
            DatadogTraceAssemblyVersion = datadogTraceAssemblyVersion;
            DatadogTraceAssemblyMvid = datadogTraceAssemblyMvid;
        }

        /// <summary>
        /// Gets schema version.
        /// </summary>
        /// <value>The schema version value.</value>
        internal string SchemaVersion { get; }

        /// <summary>
        /// Gets datadog trace assembly version.
        /// </summary>
        /// <value>The datadog trace assembly version value.</value>
        internal string DatadogTraceAssemblyVersion { get; }

        /// <summary>
        /// Gets datadog trace assembly mvid.
        /// </summary>
        /// <value>The datadog trace assembly mvid value.</value>
        internal string DatadogTraceAssemblyMvid { get; }
    }

    /// <summary>
    /// Represents duck type aot assembly metadata.
    /// </summary>
    internal readonly struct DuckTypeAotAssemblyMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuckTypeAotAssemblyMetadata"/> struct.
        /// </summary>
        /// <param name="registryAssemblyFullName">The registry assembly full name value.</param>
        /// <param name="registryAssemblyMvid">The registry assembly mvid value.</param>
        internal DuckTypeAotAssemblyMetadata(string registryAssemblyFullName, string registryAssemblyMvid)
        {
            RegistryAssemblyFullName = registryAssemblyFullName;
            RegistryAssemblyMvid = registryAssemblyMvid;
        }

        /// <summary>
        /// Gets registry assembly full name.
        /// </summary>
        /// <value>The registry assembly full name value.</value>
        internal string RegistryAssemblyFullName { get; }

        /// <summary>
        /// Gets registry assembly mvid.
        /// </summary>
        /// <value>The registry assembly mvid value.</value>
        internal string RegistryAssemblyMvid { get; }

        internal string RegistryAssemblyIdentity => $"{RegistryAssemblyFullName}; MVID={RegistryAssemblyMvid}";
    }
}
