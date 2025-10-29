// <copyright file="ServiceFabric.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    internal static class ServiceFabric
    {
        public static readonly string ServiceName = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_ServiceName");

        public static readonly string ApplicationId = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_ApplicationId");

        public static readonly string ApplicationName = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_ApplicationName");

        public static readonly string PartitionId = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_PartitionId");

        public static readonly string NodeId = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_NodeId");

        public static readonly string NodeName = EnvironmentHelpers.GetEnvironmentVariableWithLogging("Fabric_NodeName");

        public static bool IsRunningInServiceFabric() => ApplicationName is not null;
    }
}
