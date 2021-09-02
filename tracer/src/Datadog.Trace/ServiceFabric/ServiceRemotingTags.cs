// <copyright file="ServiceRemotingTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
