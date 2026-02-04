// <copyright file="PlatformKeys.ServiceFabric.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    internal static class ServiceFabric
    {
        /// <summary>
        /// The name of the Service Fabric service.
        /// </summary>
        internal const string ServiceName = "Fabric_ServiceName";

        /// <summary>
        /// The ID of the Service Fabric application.
        /// </summary>
        internal const string ApplicationId = "Fabric_ApplicationId";

        /// <summary>
        /// The name of the Service Fabric application.
        /// </summary>
        internal const string ApplicationName = "Fabric_ApplicationName";

        /// <summary>
        /// The ID of the Service Fabric partition.
        /// </summary>
        internal const string PartitionId = "Fabric_PartitionId";

        /// <summary>
        /// The ID of the Service Fabric node.
        /// </summary>
        internal const string NodeId = "Fabric_NodeId";

        /// <summary>
        /// The name of the Service Fabric node.
        /// </summary>
        internal const string NodeName = "Fabric_NodeName";
    }
}
