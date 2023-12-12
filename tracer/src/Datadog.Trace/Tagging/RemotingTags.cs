// <copyright file="RemotingTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
#pragma warning disable SA1402 // File must contain single type
    internal abstract partial class RemotingTags : InstrumentationTags
    {
        public RemotingTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        [Tag(Trace.Tags.InstrumentationName)]
        public string InstrumentationName => nameof(IntegrationId.Remoting);

        [Tag(Trace.Tags.RpcMethod)]
        public string MethodName { get; set; }

        [Tag(Trace.Tags.RpcService)]
        public string MethodService { get; set; }

        [Tag(Trace.Tags.RpcSystem)]
        public string RpcSystem => "dotnet_remoting";
    }

    internal partial class RemotingClientTags : RemotingTags
    {
        public RemotingClientTags()
            : base(SpanKinds.Client)
        {
        }
    }

    internal partial class RemotingClientV1Tags : RemotingClientTags
    {
        private string _peerServiceOverride = null;

        public RemotingClientV1Tags()
            : base()
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? MethodService;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? "peer.service"
                           : MethodService is not null
                               ? "rpc.service"
                               : null;
            }
        }
    }

    internal partial class RemotingServerTags : RemotingTags
    {
        public RemotingServerTags()
            : base(SpanKinds.Server)
        {
        }
    }
}
