// <copyright file="ServiceRemotingTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ServiceFabric
{
    internal partial class ServiceRemotingTags : InstrumentationTags
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRemotingTags"/> class.
        /// For testing purposes only. Do not use directly.
        /// </summary>
        public ServiceRemotingTags()
        {
        }

        public ServiceRemotingTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string? SpanKind { get; }

        // general Service Fabric
        [Tag(TagNames.ApplicationId)]
        public string? ApplicationId { get; set; }

        [Tag(TagNames.ApplicationName)]
        public string? ApplicationName { get; set; }

        [Tag(TagNames.PartitionId)]
        public string? PartitionId { get; set; }

        [Tag(TagNames.NodeId)]
        public string? NodeId { get; set; }

        [Tag(TagNames.NodeName)]
        public string? NodeName { get; set; }

        [Tag(TagNames.ServiceName)]
        public string? ServiceName { get; set; }

        // Service Remoting
        [Tag(TagNames.RemotingUri)]
        public string? RemotingUri { get; set; }

        [Tag(TagNames.RemotingMethodName)]
        public string? RemotingMethodName { get; set; }

        [Tag(TagNames.RemotingMethodId)]
        public string? RemotingMethodId { get; set; }

        [Tag(TagNames.RemotingInterfaceId)]
        public string? RemotingInterfaceId { get; set; }

        [Tag(TagNames.RemotingInvocationId)]
        public string? RemotingInvocationId { get; set; }

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
}
