// <copyright file="ServiceRemotingTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Internal.SourceGenerators;
using Datadog.Trace.Internal.Tagging;

namespace Datadog.Trace.Internal.ServiceFabric
{
#pragma warning disable SA1402 // File must contain single type
    internal abstract partial class ServiceRemotingTags : InstrumentationTags
    {
        public ServiceRemotingTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind { get; }

        // general Service Fabric
        [Tag(Trace.Internal.Tags.ServiceFabricApplicationId)]
        public string? ApplicationId { get; set; }

        [Tag(Trace.Internal.Tags.ServiceFabricApplicationName)]
        public string? ApplicationName { get; set; }

        [Tag(Trace.Internal.Tags.ServiceFabricPartitionId)]
        public string? PartitionId { get; set; }

        [Tag(Trace.Internal.Tags.ServiceFabricNodeId)]
        public string? NodeId { get; set; }

        [Tag(Trace.Internal.Tags.ServiceFabricNodeName)]
        public string? NodeName { get; set; }

        [Tag(Trace.Internal.Tags.ServiceFabricServiceName)]
        public string? ServiceName { get; set; }

        // Service Remoting
        [Tag(Trace.Internal.Tags.ServiceRemotingUri)]
        public string? RemotingUri { get; set; }

        [Tag(Trace.Internal.Tags.ServiceRemotingServiceName)]
        public string? RemotingServiceName { get; set; }

        [Tag(Trace.Internal.Tags.ServiceRemotingMethodName)]
        public string? RemotingMethodName { get; set; }

        [Tag(Trace.Internal.Tags.ServiceRemotingMethodId)]
        public string? RemotingMethodId { get; set; }

        [Tag(Trace.Internal.Tags.ServiceRemotingInterfaceId)]
        public string? RemotingInterfaceId { get; set; }

        [Tag(Trace.Internal.Tags.ServiceRemotingInvocationId)]
        public string? RemotingInvocationId { get; set; }
    }

    internal partial class ServiceRemotingServerTags : ServiceRemotingTags
    {
        public ServiceRemotingServerTags()
            : base(SpanKinds.Server)
        {
        }
    }

    internal partial class ServiceRemotingClientTags : ServiceRemotingTags
    {
        public ServiceRemotingClientTags()
            : base(SpanKinds.Client)
        {
        }
    }

    internal partial class ServiceRemotingClientV1Tags : ServiceRemotingClientTags
    {
        private string? _peerServiceOverride = null;

        public ServiceRemotingClientV1Tags()
            : base()
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Internal.Tags.PeerService)]
        public string? PeerService
        {
            get => _peerServiceOverride ?? RemotingServiceName ?? RemotingUri;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Internal.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : RemotingServiceName is not null
                            ? Trace.Internal.Tags.ServiceRemotingServiceName
                            : RemotingUri is not null
                                ? Trace.Internal.Tags.ServiceRemotingUri
                                : null;
            }
        }
    }
}
