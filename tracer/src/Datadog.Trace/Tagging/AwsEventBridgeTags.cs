// <copyright file="AwsEventBridgeTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsEventBridgeTags : AwsSdkTags
    {
        public AwsEventBridgeTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsEventBridgeTags(string spanKind)
        {
            SpanKind = spanKind;
        }

        // TODO rename the `rulename` tag to `eventbusname` across all runtimes
        [Tag(Trace.Tags.RuleName)]
        public string? RuleName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }

    internal partial class AwsEventBridgeV1Tags : AwsEventBridgeTags
    {
        private string? _peerServiceOverride;

        // For the sake of unit tests, define a default constructor
        // though the AWS EventBridge integration should use the constructor that takes a
        // spanKind so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsEventBridgeV1Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsEventBridgeV1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string? PeerService
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? RuleName;
            }
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string? PeerServiceSource
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride is not null
                           ? "peer.service"
                           : Trace.Tags.RuleName;
            }
        }
    }
}
