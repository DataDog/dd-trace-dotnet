// <copyright file="AwsKinesisTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Tagging
{
    internal partial class AwsKinesisTags : AwsSdkTags
    {
        private string _peerServiceOverride = null;

        public AwsKinesisTags(string spanKind)
        {
            SpanKind = spanKind;
        }

#pragma warning disable CS0618 // Remove duplicate tag
        [Tag(Trace.Tags.AwsServiceName)]
#pragma warning restore CS0618
        public override string AwsService => null;

#pragma warning disable CS0618 // Remove duplicate tag
        [Tag(Trace.Tags.AwsRegion)]
#pragma warning restore CS0618
        public override string AwsRegion => null;

        [Tag(Trace.Tags.StreamName)]
        public string StreamName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? StreamName;
            }
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride is not null
                           ? "peer.service"
                           : Trace.Tags.StreamName;
            }
        }
    }
}
