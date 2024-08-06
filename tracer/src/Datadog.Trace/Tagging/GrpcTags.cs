// <copyright file="GrpcTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Internal.Configuration;
using Datadog.Trace.Internal.SourceGenerators;

namespace Datadog.Trace.Internal.Tagging
{
#pragma warning disable SA1402 // File must contain single type
    internal abstract partial class GrpcTags : InstrumentationTags
    {
        public GrpcTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Internal.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Internal.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.Grpc);

        [Tag(Trace.Internal.Tags.GrpcMethodKind)]
        public string MethodKind { get; set; }

        [Tag(Trace.Internal.Tags.GrpcMethodName)]
        public string MethodName { get; set; }

        [Tag(Trace.Internal.Tags.GrpcMethodPath)]
        public string MethodPath { get; set; }

        [Tag(Trace.Internal.Tags.GrpcMethodPackage)]
        public string MethodPackage { get; set; }

        [Tag(Trace.Internal.Tags.GrpcMethodService)]
        public string MethodService { get; set; }

        [Tag(Trace.Internal.Tags.GrpcStatusCode)]
        public string StatusCode { get; set; }
    }

    internal partial class GrpcServerTags : GrpcTags
    {
        public GrpcServerTags()
            : base(SpanKinds.Server)
        {
        }
    }

    internal partial class GrpcClientTags : GrpcTags
    {
        public GrpcClientTags()
            : base(SpanKinds.Client)
        {
        }

        [Tag(Trace.Internal.Tags.OutHost)]
        public string Host { get; set; }

        [Tag(Trace.Internal.Tags.PeerHostname)]
        public string PeerHostname => Host;
    }

    internal partial class GrpcClientV1Tags : GrpcClientTags
    {
        private string _peerServiceOverride = null;

        public GrpcClientV1Tags()
            : base()
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Internal.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? MethodService ?? Host;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Internal.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                        ? "peer.service"
                        : MethodService is not null
                            ? "rpc.service"
                            : "out.host";
            }
        }
    }
}
