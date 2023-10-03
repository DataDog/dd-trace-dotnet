// <copyright file="AwsDynamoDbTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.SourceGenerators;

#pragma warning disable SA1402 // File must contain single type
namespace Datadog.Trace.Tagging
{
    internal partial class AwsDynamoDbTags : AwsSdkTags
    {
        [Tag(Trace.Tags.TableName)]
        public string TableName { get; set; }

        [Tag(Trace.Tags.SpanKind)]
        public override string SpanKind => SpanKinds.Client;
    }

    internal partial class AwsDynamoDbV1Tags : AwsDynamoDbTags
    {
        private string _peerServiceOverride = null;

        // Use a private setter for setting the "peer.service" tag so we avoid
        // accidentally setting the value ourselves and instead calculate the
        // value from predefined precursor attributes.
        // However, this can still be set from ITags.SetTag so the user can
        // customize the value if they wish.
        [Tag(Trace.Tags.PeerService)]
        public string PeerService
        {
            get => _peerServiceOverride ?? TableName;
            private set => _peerServiceOverride = value;
        }

        [Tag(Trace.Tags.PeerServiceSource)]
        public string PeerServiceSource
        {
            get
            {
                return _peerServiceOverride is not null
                           ? "peer.service"
                           : Trace.Tags.TableName;
            }
        }
    }
}
