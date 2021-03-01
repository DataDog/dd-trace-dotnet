#nullable enable

using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ServiceFabric
{
    internal class ServiceRemotingTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] ServiceRemotingTagProperties =
            InstrumentationTagsProperties.Concat(
                new Property<ServiceRemotingTags, string?>(TagNames.ApplicationId, t => t.ApplicationId, (t, v) => t.ApplicationId = v),
                new Property<ServiceRemotingTags, string?>(TagNames.ApplicationName, t => t.ApplicationName, (t, v) => t.ApplicationName = v),
                new Property<ServiceRemotingTags, string?>(TagNames.PartitionId, t => t.PartitionId, (t, v) => t.PartitionId = v),
                new Property<ServiceRemotingTags, string?>(TagNames.NodeId, t => t.NodeId, (t, v) => t.NodeId = v),
                new Property<ServiceRemotingTags, string?>(TagNames.NodeName, t => t.NodeName, (t, v) => t.NodeName = v),
                new Property<ServiceRemotingTags, string?>(TagNames.ServiceName, t => t.ServiceName, (t, v) => t.ServiceName = v),
                new Property<ServiceRemotingTags, string?>(TagNames.RemotingUri, t => t.RemotingUri, (t, v) => t.RemotingUri = v),
                new Property<ServiceRemotingTags, string?>(TagNames.RemotingMethodName, t => t.RemotingMethodName, (t, v) => t.RemotingMethodName = v),
                new Property<ServiceRemotingTags, string?>(TagNames.RemotingMethodId, t => t.RemotingMethodId, (t, v) => t.RemotingMethodId = v),
                new Property<ServiceRemotingTags, string?>(TagNames.RemotingInterfaceId, t => t.RemotingInterfaceId, (t, v) => t.RemotingInterfaceId = v),
                new Property<ServiceRemotingTags, string?>(TagNames.RemotingInvocationId, t => t.RemotingInvocationId, (t, v) => t.RemotingInvocationId = v));

        public ServiceRemotingTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        public override string? SpanKind { get; }

        // general Service Fabric
        public string? ApplicationId { get; set; }

        public string? ApplicationName { get; set; }

        public string? PartitionId { get; set; }

        public string? NodeId { get; set; }

        public string? NodeName { get; set; }

        public string? ServiceName { get; set; }

        // Service Remoting
        public string? RemotingUri { get; set; }

        public string? RemotingMethodName { get; set; }

        public string? RemotingMethodId { get; set; }

        public string? RemotingInterfaceId { get; set; }

        public string? RemotingInvocationId { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => ServiceRemotingTagProperties;
    }
}
