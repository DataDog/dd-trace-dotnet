// <copyright file="AwsS3Tags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsS3Tags : AwsSdkTags
    {
        public AwsS3Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsS3Tags(string spanKind)
        {
            SpanKind = spanKind;
        }

        [Tag(Trace.Tags.BucketName)]
        public string? BucketName { get; set; }

        [Tag(Trace.Tags.ObjectKey)]
        public string? ObjectKey { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind { get; }
    }

    internal partial class AwsS3V1Tags : AwsS3Tags
    {
        private string? _peerServiceOverride;

        // For the sake of unit tests, define a default constructor
        // though the AWS S3 integration should use the constructor that takes a
        // spanKind so the setter is only invoked once
        [Obsolete("Use constructor that takes a SpanKind")]
        public AwsS3V1Tags()
            : this(SpanKinds.Client)
        {
        }

        public AwsS3V1Tags(string spanKind)
            : base(spanKind)
        {
        }

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public override string? PeerService
        {
            get
            {
                if (SpanKind == SpanKinds.Consumer)
                {
                    return null;
                }

                return _peerServiceOverride ?? BucketName;
            }
            set => _peerServiceOverride = value;
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
                           : BucketName;
            }
        }
    }
}
