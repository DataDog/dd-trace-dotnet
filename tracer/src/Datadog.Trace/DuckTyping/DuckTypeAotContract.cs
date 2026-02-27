// <copyright file="DuckTypeAotContract.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.DuckTyping
{
    internal readonly struct DuckTypeAotContract
    {
        internal const string CurrentSchemaVersion = "1";

        internal DuckTypeAotContract(string schemaVersion, string datadogTraceAssemblyVersion, string datadogTraceAssemblyMvid)
        {
            SchemaVersion = schemaVersion;
            DatadogTraceAssemblyVersion = datadogTraceAssemblyVersion;
            DatadogTraceAssemblyMvid = datadogTraceAssemblyMvid;
        }

        internal string SchemaVersion { get; }

        internal string DatadogTraceAssemblyVersion { get; }

        internal string DatadogTraceAssemblyMvid { get; }
    }

    internal readonly struct DuckTypeAotAssemblyMetadata
    {
        internal DuckTypeAotAssemblyMetadata(string registryAssemblyFullName, string registryAssemblyMvid)
        {
            RegistryAssemblyFullName = registryAssemblyFullName;
            RegistryAssemblyMvid = registryAssemblyMvid;
        }

        internal string RegistryAssemblyFullName { get; }

        internal string RegistryAssemblyMvid { get; }

        internal string RegistryAssemblyIdentity => $"{RegistryAssemblyFullName}; MVID={RegistryAssemblyMvid}";
    }
}
