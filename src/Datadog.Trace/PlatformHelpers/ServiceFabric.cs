using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Util;

namespace Datadog.Trace.PlatformHelpers
{
    internal static class ServiceFabric
    {
        public static readonly string ServiceName = EnvironmentHelpers.GetEnvironmentVariable("Fabric_ServiceName");

        public static readonly string ApplicationId = EnvironmentHelpers.GetEnvironmentVariable("Fabric_ApplicationId");

        public static readonly string ApplicationName = EnvironmentHelpers.GetEnvironmentVariable("Fabric_ApplicationName");

        public static readonly string PartitionId = EnvironmentHelpers.GetEnvironmentVariable("Fabric_PartitionId");

        public static readonly string NodeId = EnvironmentHelpers.GetEnvironmentVariable("Fabric_NodeId");

        public static readonly string NodeName = EnvironmentHelpers.GetEnvironmentVariable("Fabric_NodeName");
    }
}
