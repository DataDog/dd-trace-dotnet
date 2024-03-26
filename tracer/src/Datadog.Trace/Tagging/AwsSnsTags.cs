// <copyright file="AwsSnsTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsSnsTags : AwsSdkTags
    {
        public AwsSnsTags()
            : this(SpanKinds.Client)
        {
        }

        public AwsSnsTags(string spanKind)
        {
            SpanKind = spanKind;
        }

#pragma warning disable CS0618 // Duplicate of TopicName
        [Tag(Trace.Tags.AwsTopicName)]
#pragma warning restore CS0618
        public string AwsTopicName => TopicName;

        [Tag(Trace.Tags.TopicName)]
        public string TopicName { get; set; }

        [Tag(Trace.Tags.AwsTopicArn)]
        public string TopicArn { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }

    internal partial class AwsSnsV1Tags : AwsSnsTags
    {
        private string _peerServiceOverride = null;

        // For the sake of unit tests, define a default constructor
        // though the AWS SNS integration should use the constructor that takes a spanKind
        // so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsSnsV1Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsSnsV1Tags(string spanKind)
            : base(spanKind)
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
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? TopicName;
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
                           : Trace.Tags.TopicName;
            }
        }
    }
}
