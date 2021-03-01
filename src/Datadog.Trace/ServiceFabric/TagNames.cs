namespace Datadog.Trace.ServiceFabric
{
    internal static class TagNames
    {
        // general Service Fabric
        public const string ApplicationId = "service-fabric.application-id";
        public const string ApplicationName = "service-fabric.application-name";
        public const string PartitionId = "service-fabric.partition-id";
        public const string NodeId = "service-fabric.node-id";
        public const string NodeName = "service-fabric.node-name";
        public const string ServiceName = "service-fabric.service-name";

        // Service Remoting
        public const string RemotingUri = "service-fabric.service-remoting.uri";
        public const string RemotingMethodName = "service-fabric.service-remoting.method-name";
        public const string RemotingMethodId = "service-fabric.service-remoting.method-id";
        public const string RemotingInterfaceId = "service-fabric.service-remoting.interface-id";
        public const string RemotingInvocationId = "service-fabric.service-remoting.invocation-id";
    }
}
