// <copyright file="ServiceFabric.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    internal static class ServiceFabric
    {
        public static readonly string ServiceName = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.ServiceName);

        public static readonly string ApplicationId = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.ApplicationId);

        public static readonly string ApplicationName = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.ApplicationName);

        public static readonly string PartitionId = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.PartitionId);

        public static readonly string NodeId = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.NodeId);

        public static readonly string NodeName = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.ServiceFabric.NodeName);

        public static bool IsRunningInServiceFabric() => ApplicationName is not null;
    }
}
